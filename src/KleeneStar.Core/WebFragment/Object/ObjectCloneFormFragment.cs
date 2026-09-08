using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Provides a form fragment for cloning objects, dynamically building the form 
    /// structure from the configured edit form of the object's class.
    /// </summary>
    [Title("kleenestar.core:object.clone.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Clone>]
    [Cache]
    public sealed class ObjectCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the summary of the object. This
        /// system field is always rendered first because every object carries a summary,
        /// regardless of the form configuration.
        /// </summary>
        public ControlDataFormItemInputUnique Summary { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Object.Summary),
            Label = _ => "kleenestar.core:object.summary.label",
            Placeholder = _ => "kleenestar.core:object.summary.placeholder",
            Help = _ => "kleenestar.core:object.summary.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the object. This
        /// system field is rendered after the summary when no edit form structure is
        /// configured on the class.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Object.Description),
            Label = _ => "kleenestar.core:object.description.label",
            Placeholder = _ => "kleenestar.core:object.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();
            ItemId = renderContext =>
            {
                var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
                var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);
                return @object?.Id.ToString();
            };
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParam = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParam);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
            var items = BuildItems(@object, identityId);

            return base.Render(renderContext, visualTree, items);
        }

        /// <summary>
        /// Builds the form items from the configured edit form. The system field
        /// <see cref="Summary"/> is always emitted first; the rest of the structure
        /// is reproduced from the form's tabs, groups, and field references. When no
        /// active edit form exists, only the system fields are rendered.
        /// </summary>
        private IEnumerable<IControlFormItem> BuildItems(Model.Entities.Object @object, Guid identityId)
        {
            yield return Summary;

            // a copy inherits the classification of its original, which the notice says out
            // loud. Changing it is not this form's business - see the edit form
            var notice = @object is not null
                ? ObjectFormLayout.CreateSecurityLevelNotice(@object.ClassId, identityId, @object.SecurityLevelId)
                : null;

            if (notice is not null)
            {
                yield return notice;
            }

            var form = @object is not null ? ResolveStandardForm(@object.ClassId, FormType.Edit) : null;

            if (form is null || form.Tabs is null || form.Tabs.Count == 0)
            {
                yield return Description;
                yield break;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToDictionary(f => f.Id);

            var orderedTabs = form.Tabs.OrderBy(t => t.Position).ToList();

            if (orderedTabs.Count == 1)
            {
                // Single-tab forms do not need a tab bar; emit the tab elements
                // directly so the form reads naturally.
                foreach (var element in orderedTabs[0].Elements.OrderBy(e => e.Position))
                {
                    var item = BuildElement(element, fields);
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
                yield break;
            }

            var tabGroup = new ControlFormItemGroupTab()
            {
                Layout = _ => TypeLayoutTab.Underline
            };

            foreach (var t in orderedTabs)
            {
                var view = new ControlFormItemGroupTabView("tab-" + t.Id.ToString("N"))
                {
                    Title = _ => t.Name
                };

                foreach (var element in t.Elements.OrderBy(e => e.Position))
                {
                    var item = BuildElement(element, fields);
                    if (item is not null)
                    {
                        view.Add(item);
                    }
                }

                tabGroup.AddView(view);
            }

            yield return tabGroup;
        }

        /// <summary>
        /// Resolves the active standard form of the requested type for the given class.
        /// </summary>
        /// <param name="classId">The unique identifier of the class.</param>
        /// <param name="type">The desired form type (typically <see cref="FormType.Edit"/>).</param>
        /// <returns>
        /// The form together with its full structural tree, or <c>null</c> when no
        /// matching active form exists.
        /// </returns>
        private static Model.Entities.Form ResolveStandardForm(Guid classId, FormType type)
        {
            var form = CoreHub.FormManager
                .GetForms(new ClassIdParameter(classId))
                .FirstOrDefault(f => f.FormType == type && f.State == FormState.Active);

            return form is null ? null : CoreHub.FormManager.GetFormWithStructure(form.Id);
        }

        /// <summary>
        /// Recursively maps a form element from the model to a form item control. Field
        /// references resolve to a typed input matching the referenced
        /// <see cref="Field.FieldType"/>; group elements map to the layout group control
        /// matching <see cref="FormGroupElement.Layout"/> and recurse into their children.
        /// </summary>
        /// <param name="element">The form element to convert.</param>
        /// <param name="fields">
        /// Lookup of active fields of the class, used to decorate field references with
        /// their display metadata. Inactive or deprecated fields are skipped.
        /// </param>
        /// <returns>
        /// The corresponding form item, or <c>null</c> when the element type is not
        /// supported or the referenced field is not visible on the form.
        /// </returns>
        private static IControlFormItem BuildElement(FormElement element, IDictionary<Guid, Model.Entities.Field> fields)
        {
            if (element is FormFieldRefElement fieldRef)
            {
                return fields.TryGetValue(fieldRef.FieldId, out var field)
                    ? CreateInputForField(field)
                    : null;
            }

            if (element is FormGroupElement group)
            {
                var groupControl = CreateGroupForLayout(group.Layout);

                foreach (var child in group.Children.OrderBy(c => c.Position))
                {
                    var childItem = BuildElement(child, fields);
                    if (childItem is not null)
                    {
                        groupControl.Add(childItem);
                    }
                }

                return groupControl;
            }

            return null;
        }

        /// <summary>
        /// Creates an empty layout group matching the requested <see cref="FormGroupLayout"/>.
        /// </summary>
        private static ControlFormItemGroup CreateGroupForLayout(FormGroupLayout layout)
        {
            return layout switch
            {
                FormGroupLayout.Horizontal => new ControlFormItemGroupHorizontal(),
                FormGroupLayout.Mix => new ControlFormItemGroupMix(),
                FormGroupLayout.ColumnVertical => new ControlFormItemGroupColumnVertical(),
                FormGroupLayout.ColumnHorizontal => new ControlFormItemGroupColumnHorizontal(),
                FormGroupLayout.ColumnMix => new ControlFormItemGroupColumnMix(),
                _ => new ControlFormItemGroupVertical(),
            };
        }

        /// <summary>
        /// Creates a typed input control for the given field, mapping
        /// <see cref="Field.FieldType"/> to the matching WebUI form item.
        /// </summary>
        private static IControlFormItem CreateInputForField(Model.Entities.Field field)
        {
            switch (field.FieldType)
            {
                case FieldType.Boolean:
                    return new ControlFormItemInputCheck()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Date:
                    return new ControlFormItemInputDate()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Selection:
                    var combo = new ControlFormItemInputCombo()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        combo.Add(new ControlFormItemInputComboItem()
                        {
                            Text = _ => option,
                            Value = _ => option
                        });
                    }
                    return combo;

                case FieldType.Priority:
                    var priorityCombo = new ControlFormItemInputCombo()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    foreach (var priority in ResolveFieldPriorities(field))
                    {
                        priorityCombo.Add(new ControlFormItemInputComboItem()
                        {
                            Text = _ => priority.Name,
                            Value = _ => priority.Name
                        });
                    }
                    return priorityCombo;

                case FieldType.Tag:
                    return new ControlFormItemInputTag()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Attachment:
                    return new ControlFormItemInputFile()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Number:
                case FieldType.Reference:
                case FieldType.Workflow:
                case FieldType.User:
                case FieldType.Text:
                default:
                    return new ControlFormItemInputText()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
            }
        }

        /// <summary>
        /// Resolves the priorities offered for a priority-typed field. When the field
        /// configuration restricts the field to a specific set of priorities
        /// (<see cref="Model.Entities.Field.SelectedPriorityIds"/>), only those are returned;
        /// otherwise every active priority of the field's class is offered. The result is
        /// ordered by the priority display order.
        /// </summary>
        /// <param name="field">The priority-typed field whose options are resolved.</param>
        /// <returns>The priorities to present, in display order.</returns>
        private static IEnumerable<Model.Entities.Priority> ResolveFieldPriorities(Model.Entities.Field field)
        {
            // Load the field's class priorities in a single round-trip and filter in memory.
            // The previous per-id GetPriority(id) loop opened a fresh DbContext and ran one
            // query for every selected id; priorities are class-scoped, so the selected ids
            // are a subset of the class priorities loaded here.
            var priorities = CoreHub.PriorityManager
                .GetPriorities(new ClassIdParameter(field.ClassId));

            if (field.SelectedPriorityIds is { Count: > 0 })
            {
                var selected = field.SelectedPriorityIds.ToHashSet();
                return priorities
                    .Where(p => selected.Contains(p.Id))
                    .OrderBy(p => p.Order);
            }

            return priorities
                .Where(p => p.State == PriorityState.Active)
                .OrderBy(p => p.Order);
        }
    }
}
