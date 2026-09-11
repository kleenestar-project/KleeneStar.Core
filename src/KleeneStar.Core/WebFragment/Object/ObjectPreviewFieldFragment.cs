using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The field values of the reduced object view: the fields of the view form configured on
    /// the object's class, rendered as read-only name/value attributes.
    /// </summary>
    /// <remarks>
    /// <see cref="ObjectItemDetailFragment"/> renders the same form as a column of
    /// <see cref="ControlSmartEdit"/> wrappers - one live input per field, each with its own
    /// persistence round-trip - and reproduces the form's tab structure. Neither survives the
    /// move into a detail pane: a tab strip inside a pane that is itself one side of a split
    /// reads as a second navigation, and an editor per field is a wide control in a narrow
    /// column. The reduced view flattens the tabs into one sequence and shows the values as
    /// text.
    /// <para>
    /// The two views also disagree about what to leave out, and deliberately so. The reading
    /// view drops workflow and date fields because its property column carries them; the pane
    /// never receives that column, so the reduced view keeps the date fields and drops only the
    /// workflow fields - the status is already in the headline beside the summary, rendered by
    /// <see cref="ObjectMetadataStatusFragment"/>. A field the object has never filled is left
    /// out here as well, which is the reduction that scales: it is the modelled field count of
    /// the class that makes a pane long, and an empty row is the one that earns none of it.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(2)]
    [Cache]
    public sealed class ObjectPreviewFieldFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IFieldManager _fieldManager;
        private readonly IValueManager _valueManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <remarks>
        /// The view form is not read through an injected manager: which form is the class's is
        /// a decision <see cref="ObjectFormLayout.ResolveStandardForm"/> owns for every surface
        /// that presents a form, and a second copy of it here is how the pane and the sheet
        /// would come to answer differently.
        /// </remarks>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        /// <param name="valueManager">The value manager used to read the object's field values.</param>
        public ObjectPreviewFieldFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IFieldManager fieldManager,
            IValueManager valueManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _fieldManager = fieldManager;
            _valueManager = valueManager;
        }

        /// <summary>
        /// Renders the field values. Returns <c>null</c> when the fragment's render conditions
        /// exclude it, when no object can be resolved from the request, or when the object has
        /// no value the reduced view shows.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var attributes = BuildAttributes(@object).ToList();

            if (attributes.Count == 0)
            {
                // a class without a view form, or an object that has filled none of its fields,
                // gets no empty section - the identity block above already names the object
                return null;
            }

            var section = new ControlSection("object-preview-field-section")
            {
                Header = _ => "kleenestar.core:object.detail.card.header",
                HeaderIcon = _ => new IconTableList(),
                Layout = _ => TypeLayoutSection.Rule
            };

            foreach (var attribute in attributes)
            {
                section.Add(attribute);
            }

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds one read-only attribute per field the reduced view shows, in the order the
        /// view form declares them across all of its tabs.
        /// </summary>
        /// <param name="object">The object whose values are shown.</param>
        /// <returns>The attributes; empty when the class has no active view form.</returns>
        private IEnumerable<IControl> BuildAttributes(Model.Entities.Object @object)
        {
            // the same resolution the sheet of a form-rendered object performs, through the
            // one helper both go by - which view form is the class's, and loaded with its
            // structure - so the pane and the sheet cannot come to disagree about it
            var form = ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.View);

            if (form?.Tabs is null || form.Tabs.Count == 0)
            {
                yield break;
            }

            var fields = _fieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active)
                .ToDictionary(x => x.Id);

            var values = _valueManager
                .GetValues(@object.Id)
                .GroupBy(x => x.FieldId)
                .ToDictionary(x => x.Key, x => x.First());

            var references = form.Tabs
                .OrderBy(x => x.Position)
                .SelectMany(x => Flatten(x.Elements));

            foreach (var reference in references)
            {
                if (!fields.TryGetValue(reference.FieldId, out var field))
                {
                    continue;
                }

                if (string.Equals(field.Name, nameof(Model.Entities.Object.Description), StringComparison.OrdinalIgnoreCase))
                {
                    // the description is a system attribute of the object and is rendered by
                    // ObjectPreviewDescriptionFragment; a field aliasing it would show it twice
                    continue;
                }

                if (field.FieldType == FieldType.Workflow)
                {
                    // the status sits in the headline beside the summary
                    continue;
                }

                values.TryGetValue(field.Id, out var value);

                if (string.IsNullOrWhiteSpace(value?.Data))
                {
                    // the reading view shows an unset field as an empty editor, because there it
                    // is an invitation to fill it in. here it is a row that says nothing, and a
                    // class with fifty modelled fields would bury the ones that do
                    continue;
                }

                var data = value.Data;

                yield return new ControlAttribute("object-preview-field-" + field.Id.ToString("N"))
                {
                    Icon = _ => new IconAngleRight(),
                    Key = _ => field.Name,
                    Value = ctx => ObjectValueFormat.Format(ctx, field, data)
                };
            }
        }

        /// <summary>
        /// Yields every field reference of the supplied element tree in document order,
        /// descending into groups. The reduced view shows one flat sequence, so the group
        /// containers themselves are not emitted.
        /// </summary>
        /// <param name="elements">The elements to walk.</param>
        /// <returns>The field references.</returns>
        private static IEnumerable<FormFieldRefElement> Flatten(IEnumerable<FormElement> elements)
        {
            foreach (var element in (elements ?? []).OrderBy(x => x.Position))
            {
                if (element is FormFieldRefElement fieldRef)
                {
                    yield return fieldRef;
                }
                else if (element is FormGroupElement group)
                {
                    foreach (var inner in Flatten(group.Children))
                    {
                        yield return inner;
                    }
                }
            }
        }
    }
}
