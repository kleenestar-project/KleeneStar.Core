using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    // The entity type name collides with the KleeneStar.Core.WWW.Field namespace segment of
    // the same name; alias it inside the namespace block so Field resolves to the model
    // entity here (see also the Calendar namespace-collision note).
    using Field = KleeneStar.Model.Entities.Field;

    /// <summary>
    /// Headline-metadata fragment that surfaces the current workflow status of the object as
    /// a read-only pill badge, on the reading views and on the reduced view
    /// <see cref="WWW.Issue._objectkey_.Preview"/> a master-detail pane shows.
    /// </summary>
    /// <remarks>
    /// The status is resolved exactly like the interactive
    /// <see cref="ObjectPropertyWorkflowCardFragment"/> (the persisted
    /// <see cref="Model.Entities.Value"/> of every workflow-backed field matched against the
    /// attached workflow's statuses by <see cref="IWorkflowManager.ResolveStatus"/>) and
    /// carries the color of the status category, but is rendered as a plain badge without the
    /// split button, dropdown or workflow modal — so the status is displayed but cannot be
    /// changed from here. Returns <c>null</c> when the class has no workflow field or none has
    /// a value yet, keeping the metadata line clean.
    /// <para>
    /// A read-only badge is exactly what the reduced view needs, so it renders there unchanged:
    /// the detail pane never receives the property column that holds the interactive control,
    /// and this fragment is what puts the status in front of a reader without it.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlineMetadata>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(0)]
    [Cache]
    public sealed class ObjectMetadataStatusFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IFieldManager _fieldManager;
        private readonly IWorkflowManager _workflowManager;
        private readonly IValueManager _valueManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        /// <param name="workflowManager">The workflow manager used to load the workflow
        /// attached to a workflow-type field.</param>
        /// <param name="valueManager">The value manager used to read the object's current
        /// field values.</param>
        public ObjectMetadataStatusFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IFieldManager fieldManager,
            IWorkflowManager workflowManager,
            IValueManager valueManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _fieldManager = fieldManager;
            _workflowManager = workflowManager;
            _valueManager = valueManager;
        }

        /// <summary>
        /// Renders the read-only status badge(s) for the current object. Returns <c>null</c>
        /// when the fragment's render conditions exclude it, when no object can be resolved
        /// from the request, or when no workflow status can be determined.
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

            var workflowFields = _fieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated
                    && f.State == FieldState.Active
                    && f.FieldType == FieldType.Workflow
                    && f.WorkflowId.HasValue)
                .ToList();

            if (workflowFields.Count == 0)
            {
                return null;
            }

            var panel = new ControlFlex("object-metadata-status")
            {
                Layout = _ => TypeLayoutFlex.Default,
                Align = _ => TypeAlignFlex.Center,
                Justify = _ => TypeJustifiedFlex.Start
            };

            var rendered = false;

            foreach (var field in workflowFields)
            {
                var badge = BuildStatusBadge(@object, field);
                if (badge is not null)
                {
                    panel.Add(badge);
                    rendered = true;
                }
            }

            return rendered ? panel.Render(renderContext, visualTree) : null;
        }

        /// <summary>
        /// Builds the read-only status badge for a single workflow-backed field, or
        /// <c>null</c> when the workflow cannot be resolved or the field has no value yet.
        /// </summary>
        /// <param name="object">The object whose status is displayed.</param>
        /// <param name="field">The workflow-type field being rendered.</param>
        /// <returns>The badge control, or <c>null</c>.</returns>
        private IControl BuildStatusBadge(Model.Entities.Object @object, Field field)
        {
            // the states are needed to resolve the payload, so the structural load is used
            // rather than the shallow header read
            var workflow = _workflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

            if (workflow is null)
            {
                return null;
            }

            var value = _valueManager.GetValue(@object.Id, field.Id);
            var current = _workflowManager.ResolveStatus(workflow, value?.Data);
            var currentLabel = current?.Name
                ?? (string.IsNullOrWhiteSpace(value?.Data) ? null : value.Data);

            if (string.IsNullOrWhiteSpace(currentLabel))
            {
                return null;
            }

            var color = current?.Category?.Color;

            return new ControlBadge("object-metadata-status-" + field.Id.ToString("N"))
            {
                Value = _ => currentLabel,
                Pill = _ => TypePillBadge.Pill,
                BackgroundColor = _ => string.IsNullOrWhiteSpace(color)
                    ? new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Info)
                    : new PropertyColorBackgroundBadge(color),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.One, PropertySpacing.Space.None, PropertySpacing.Space.None)
            };
        }
    }
}
