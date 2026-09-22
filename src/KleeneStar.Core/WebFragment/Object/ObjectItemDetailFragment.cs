using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The metadata section of the object view: the field structure of the object as the
    /// view form of its class arranges it.
    /// </summary>
    /// <remarks>
    /// The control resolves the object addressed by the current request, looks up the
    /// <see cref="FormType.View"/> form configured on its class, and renders that
    /// structure as a read-only view that mirrors the form's tabs, layout groups, and
    /// field references. Unlike <see cref="ObjectEditFormFragment"/> the
    /// view is not a <c>&lt;form&gt;</c> with validation and a submit panel; instead
    /// each value is wrapped in a <see cref="ControlSmartEdit"/> that persists changes
    /// inline via the object REST API as soon as the user finishes editing.
    /// The field structure is hosted inside a single <see cref="ControlSection"/>, so it
    /// folds away as a whole; the object description, the comment thread, and the comment
    /// composer are surfaced separately by <see cref="ObjectDescriptionCardFragment"/>,
    /// <see cref="ObjectCommentCardFragment"/>, and
    /// <see cref="ObjectCommentComposerCardFragment"/>.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Cache]
    public sealed class ObjectItemDetailFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation.
        /// Cannot be null.
        /// </param>
        public ObjectItemDetailFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation. The object's field structure
        /// (derived from the active <see cref="FormType.View"/> form of its class) is
        /// rendered inside a single <see cref="ControlSection"/>. When no view form is
        /// configured, or the form places no field the section would show, nothing is
        /// rendered: a captioned section with nothing under it reads as a fault, and the
        /// description, comment thread, and composer are surfaced separately by their own
        /// fragments either way.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered section, or <c>null</c> when no
        /// object can be resolved from the request or the section would be empty.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParam = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParam);
            var role = Role?.Invoke(renderContext);

            if (@object is null)
            {
                return null;
            }

            var objectUri = ResolveObjectRestUri(@object, renderContext);

            var body = new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("wx-kleenestar-object-detail", GetClasses(renderContext)),
                Style = GetStyles(renderContext),
                Role = role,
                //DataTheme = Theme.ToValue()
            };

            body.AddUserAttribute("data-object-id", @object.Id.ToString());
            body.AddUserAttribute("data-object-key", @object.Key);
            body.AddUserAttribute("data-rest-url", objectUri?.ToString());

            if (!AddFieldStructure(body, @object, objectUri, renderContext, visualTree))
            {
                return null;
            }

            var section = new ControlSection("object-detail-section")
            {
                Header = _ => "kleenestar.core:object.detail.card.header",
                HeaderIcon = _ => new IconTableList(),
                Layout = _ => TypeLayoutSection.Rule
            };

            // the field structure is where two people edit the same object at the same time -
            // every value here is a smart-edit that writes through on blur - so it is the
            // surface that carries the presence, the pointers and the carets of whoever else
            // has the object open
            section.Add(ObjectCollaborativeHost.Create
            (
                @object,
                "detail",
                renderContext,
                new ControlHtml("object-detail-body-" + @object.Id.ToString("N"))
                {
                    Html = _ => body.ToString()
                }
            ));

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the active <see cref="FormType.View"/> form for the object's class and
        /// appends its field structure to the supplied detail body: a single field list
        /// when the form defines one tab, or a tab control when it defines several. When
        /// no active view form (or no tab) is configured, or every field the form places is
        /// one the list skips - a system alias, a workflow or date field shown elsewhere - the
        /// body is left as it was and the caller renders no section at all.
        /// </summary>
        /// <param name="body">The detail body container the field structure is appended to.</param>
        /// <param name="object">The object whose field values are displayed.</param>
        /// <param name="objectUri">The REST URI bound to the object's id; used by the
        /// inline smart-edit controls to persist value changes.</param>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns><see langword="true"/> when at least one field row was appended.</returns>
        private static bool AddFieldStructure
        (
            HtmlElementTextContentDiv body,
            Model.Entities.Object @object,
            IUri objectUri,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            var form = ResolveStandardForm(@object.ClassId, FormType.View);

            if (form is null || form.Tabs is null || form.Tabs.Count == 0)
            {
                return false;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToDictionary(f => f.Id);

            var values = LoadValuesForObject(@object.Id);

            var orderedTabs = form.Tabs.OrderBy(t => t.Position).ToList();

            if (orderedTabs.Count == 1)
            {
                var list = BuildFieldList(orderedTabs[0].Elements, fields, values, @object, objectUri);
                if (list is null)
                {
                    return false;
                }

                body.Add(list.Render(renderContext, visualTree));

                return true;
            }

            var tabControl = new ControlTab("tabs-" + @object.Id.ToString("N"))
            {
                Layout = _ => TypeLayoutTab.Underline
            };
            var populated = false;

            foreach (var t in orderedTabs)
            {
                var view = new ControlTabView("tab-" + t.Id.ToString("N"))
                {
                    Title = _ => t.Name
                };

                var tabList = BuildFieldList(t.Elements, fields, values, @object, objectUri);
                if (tabList is not null)
                {
                    view.Add(tabList);
                    populated = true;
                }

                tabControl.Add(view);
            }

            // a tab strip over nothing but empty tabs is the empty section with more chrome
            if (!populated)
            {
                return false;
            }

            body.Add(tabControl.Render(renderContext, visualTree));

            return true;
        }

        /// <summary>
        /// Flattens the supplied form elements (descending recursively into groups) into a
        /// vertical list of "field name: value" rows stacked one below the other. Each row
        /// shows the field name (with the field description as a native HTML <c>title</c>
        /// tooltip) followed by the inline-editable smart-edit bound to the value. Field
        /// references whose name aliases a system attribute already rendered outside the form
        /// (currently only <see cref="Model.Entities.Object.Description"/>) are skipped to
        /// avoid the duplicate render path, as are <see cref="FieldType.Workflow"/> fields
        /// (surfaced by <see cref="ObjectPropertyWorkflowCardFragment"/>) and
        /// <see cref="FieldType.Date"/> fields (surfaced by
        /// <see cref="ObjectPropertyDateCardFragment"/>). Group labels are not surfaced — the
        /// list intentionally collapses the form's visual structure into a flat sequence of
        /// name/value rows.
        /// </summary>
        /// <returns>The field-list panel, or <c>null</c> when no visible field rows remain
        /// after filtering.</returns>
        private static IControl BuildFieldList
        (
            IEnumerable<FormElement> elements,
            IDictionary<Guid, Model.Entities.Field> fields,
            IDictionary<Guid, Value> values,
            Model.Entities.Object @object,
            IUri objectUri
        )
        {
            var blocks = new List<IControl>();

            foreach (var fieldRef in FlattenFieldRefs(elements))
            {
                if (!fields.TryGetValue(fieldRef.FieldId, out var field))
                {
                    continue;
                }

                if (IsSystemAttributeAlias(field.Name))
                {
                    continue;
                }

                if (field.FieldType == FieldType.Workflow)
                {
                    // Workflow-backed status fields are surfaced separately by
                    // ObjectPropertyWorkflowCardFragment as a split button in the property
                    // card, so they are intentionally omitted from the inline detail list.
                    continue;
                }

                if (field.FieldType == FieldType.Date)
                {
                    // Date fields are surfaced separately by ObjectPropertyDateCardFragment
                    // in the property column, so they are omitted from the inline detail list.
                    continue;
                }

                values.TryGetValue(field.Id, out var value);
                blocks.Add(BuildFieldBlock(@object, objectUri, field, value));
            }

            if (blocks.Count == 0)
            {
                return null;
            }

            return new ControlPanel("field-list-" + @object.Id.ToString("N"), [.. blocks])
            {
                Classes = ["wx-kleenestar-field-list"]
            };
        }

        /// <summary>
        /// Recursively yields every <see cref="FormFieldRefElement"/> contained in the
        /// supplied element tree, in document order honouring <see cref="FormElement.Position"/>.
        /// Group containers are descended into but not emitted themselves.
        /// </summary>
        private static IEnumerable<FormFieldRefElement> FlattenFieldRefs(IEnumerable<FormElement> elements)
        {
            foreach (var element in elements.OrderBy(e => e.Position))
            {
                if (element is FormFieldRefElement fieldRef)
                {
                    yield return fieldRef;
                }
                else if (element is FormGroupElement group)
                {
                    foreach (var inner in FlattenFieldRefs(group.Children))
                    {
                        yield return inner;
                    }
                }
            }
        }

        /// <summary>
        /// Loads the persisted field values of the supplied object via
        /// <see cref="CoreHub.ValueManager"/> and returns them keyed by
        /// <see cref="Value.FieldId"/>. The smart-edit controls use this map to
        /// initialize their inputs so the inline editor shows the current value instead
        /// of being empty on first render.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The value map; empty when the object has no stored values yet.</returns>
        private static IDictionary<Guid, Value> LoadValuesForObject(Guid objectId)
        {
            return CoreHub.ValueManager
                .GetValues(objectId)
                .GroupBy(v => v.FieldId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>
        /// Returns <c>true</c> when the supplied field name aliases a system attribute
        /// of <see cref="Model.Entities.Object"/> that is rendered outside the form
        /// structure (currently only <see cref="Model.Entities.Object.Description"/>).
        /// </summary>
        /// <param name="fieldName">The field name as configured on the class.</param>
        /// <returns><c>true</c> when the field duplicates a system attribute.</returns>
        private static bool IsSystemAttributeAlias(string fieldName)
        {
            return string.Equals(fieldName, nameof(Model.Entities.Object.Description), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a single "field name: value" row: the field name (carrying the field
        /// description as a native HTML <c>title</c> tooltip, with a trailing asterisk for
        /// required fields) followed by the inline-editable smart-edit bound to the value.
        /// The row is laid out as a flex line so the label and the editable value sit next to
        /// each other; consecutive rows stack vertically.
        /// </summary>
        /// <remarks>
        /// When a <paramref name="value"/> exists for the field, it is converted to the
        /// matching <see cref="ControlFormInputValue{T}"/> type and pushed into the input
        /// via <see cref="ControlSmartEdit.Initialize(System.Action{System.Object})"/>;
        /// without this step the input renders empty regardless of the persisted value.
        /// </remarks>
        private static IControl BuildFieldBlock(Model.Entities.Object @object, IUri objectUri, Model.Entities.Field field, Value value)
        {
            var input = CreateInputForField(field);

            var smartEdit = new ControlSmartEdit("field-" + field.Id.ToString("N"))
            {
                ObjectId = _ => @object.Id.ToString(),
                ObjectName = _ => field.Name,
                Uri = _ => objectUri,
                Method = _ => RequestMethod.PUT
            };

            smartEdit.Add(input);

            var initialValue = BuildInputValue(field, value?.Data);
            if (initialValue is not null)
            {
                smartEdit.Initialize(args => args.SetValue(input, initialValue));
            }

            var label = new ControlHtml("field-label-" + field.Id.ToString("N"))
            {
                Html = _ =>
                {
                    var span = new HtmlElementTextSemanticsSpan
                    {
                        Class = "wx-kleenestar-field-label"
                    };

                    if (!ProseText.IsEmpty(field.Description))
                    {
                        span.AddUserAttribute("title", ProseText.ToPlainText(field.Description));
                    }

                    span.Add(new HtmlText(field.Name + (field.Required ? " *" : "") + ":"));
                    return span.ToString();
                }
            };

            // the row carries no layout of its own: the field list is a two-column grid and the
            // row dissolves into it (display: contents), so every label lines up on one edge
            // however long the one above it ran
            return new ControlPanel("field-row-" + field.Id.ToString("N"), label, smartEdit)
            {
                Classes = ["wx-kleenestar-field"]
            };
        }

        /// <summary>
        /// Converts the persisted <see cref="Value.Data"/> string of a field into the
        /// strongly-typed input value that matches the input control produced by
        /// <see cref="CreateInputForField(Model.Entities.Field)"/>. Boolean and date
        /// fields parse the raw payload, tag fields split on commas, everything else
        /// falls through to a plain string value.
        /// </summary>
        /// <param name="field">The field whose input is being initialised.</param>
        /// <param name="data">The persisted value payload; <c>null</c> or empty for a
        /// field that has not been set yet.</param>
        /// <returns>The input-value wrapper, or <c>null</c> when no value should be pushed
        /// into the input (currently only for <see cref="FieldType.Attachment"/>).</returns>
        private static IControlFormInputValue BuildInputValue(Model.Entities.Field field, string data)
        {
            switch (field.FieldType)
            {
                case FieldType.Boolean:
                    return new ControlFormInputValueBool(
                        bool.TryParse(data, out var b) && b);

                case FieldType.Date:
                    if (string.IsNullOrEmpty(data))
                    {
                        return new ControlFormInputValueDate((DateTime?)null);
                    }
                    return DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                        ? new ControlFormInputValueDate(dt)
                        : new ControlFormInputValueDate((DateTime?)null);

                case FieldType.Tag:
                    var items = string.IsNullOrEmpty(data)
                        ? []
                        : data.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return new ControlFormInputValueStringList(items);

                case FieldType.Attachment:
                    // attachments are referenced through their own upload pipeline, not
                    // through the inline smart-edit value initialisation.
                    return null;

                case FieldType.Number:
                case FieldType.Reference:
                case FieldType.Selection:
                case FieldType.Workflow:
                case FieldType.User:
                case FieldType.Text:
                default:
                    return new ControlFormInputValueString(data ?? string.Empty);
            }
        }

        /// <summary>
        /// Resolves the active standard form of the requested type for the given class.
        /// </summary>
        private static Model.Entities.Form ResolveStandardForm(Guid classId, FormType type)
        {
            var form = CoreHub.FormManager
                .GetForms(new ClassIdParameter(classId))
                .FirstOrDefault(f => f.FormType == type && f.State == FormState.Active);

            return form is null ? null : CoreHub.FormManager.GetFormWithStructure(form.Id);
        }

        /// <summary>
        /// Returns the REST endpoint that owns the object's persistence, augmented with
        /// the object's id so smart-edit PUTs target the right record.
        /// </summary>
        private static IUri ResolveObjectRestUri(Model.Entities.Object @object, IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();
            if (uri is null)
            {
                return null;
            }

            var withQuery = uri.Add(new UriQuery("id", @object.Id.ToString()));

            return renderContext?.Request is null
                ? withQuery
                : withQuery.BindParameters(renderContext.Request);
        }


        /// <summary>
        /// Creates a typed input control for the given field, mirroring the mapping used
        /// by the edit form so the inline editor presents the same widget the user knows
        /// from the modal.
        /// </summary>
        /// <remarks>
        /// The label is always the field name; the <c>Help</c> property of each input is
        /// driven by <see cref="Field.Description"/> so the description renders as the
        /// tooltip on the help icon next to the label. <see cref="Field.HelpText"/> is
        /// surfaced through the input's <c>Description</c> property (where supported) so
        /// the longer inline help text stays visible beneath the value.
        /// </remarks>
        private static IControlFormItemInput CreateInputForField(Model.Entities.Field field)
        {
            switch (field.FieldType)
            {
                case FieldType.Boolean:
                    return new ControlFormItemInputCheck()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Description = _ => field.HelpText,
                        Help = _ => ProseText.ToPlainText(field.Description),
                        Required = _ => field.Required
                    };

                case FieldType.Date:
                    return new ControlFormItemInputDate()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Description = _ => field.HelpText,
                        Help = _ => ProseText.ToPlainText(field.Description),
                        Required = _ => field.Required
                    };

                case FieldType.Selection:
                    var combo = new ControlFormItemInputCombo()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => ProseText.ToPlainText(field.Description),
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
                        Help = _ => ProseText.ToPlainText(field.Description),
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
                        Help = _ => ProseText.ToPlainText(field.Description),
                        Required = _ => field.Required
                    };

                case FieldType.Attachment:
                    return new ControlFormItemInputFile()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => ProseText.ToPlainText(field.Description),
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
                        Description = _ => field.HelpText,
                        Help = _ => ProseText.ToPlainText(field.Description),
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
