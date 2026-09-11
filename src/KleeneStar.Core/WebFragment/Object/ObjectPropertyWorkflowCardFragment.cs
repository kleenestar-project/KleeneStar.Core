using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    // The entity type names collide with the KleeneStar.Core.WWW.* namespace segments of
    // the same name; alias them inside the namespace block so Field/Status/Workflow resolve
    // to the model entities here (see also the Calendar namespace-collision note).
    using Field = KleeneStar.Model.Entities.Field;
    using Status = KleeneStar.Model.Entities.Status;
    using Workflow = KleeneStar.Model.Entities.Workflow;

    /// <summary>
    /// The status section of the reference zone, rendering for every workflow-backed field of the
    /// current object's class, a split button reflecting the field's current status on
    /// <see cref="WWW.Issue._objectkey_.Index"/> and offering the states the object may be
    /// moved to next.
    /// </summary>
    /// <remarks>
    /// A field qualifies when it is active, not deprecated, of type
    /// <see cref="FieldType.Workflow"/>, and carries a <see cref="Field.WorkflowId"/>.
    /// For each such field the section hosts a <see cref="ControlSplitButton"/> whose main
    /// button shows the field's current status (resolved from the persisted
    /// <see cref="Model.Entities.Value"/> against the workflow's states) in the color and with
    /// the glyph of the status category, and whose dropdown lists the states reachable from it
    /// through the workflow's active transitions. Choosing one drives
    /// <see cref="WWW.Api._1_.Transitions._objectkey_.Index"/>, which asks
    /// <see cref="IWorkflowManager.ExecuteTransition"/> to run the state change - guard,
    /// validators, value write, post functions - and redirects back here. The last dropdown
    /// item opens the workflow itself in a modal
    /// (<see cref="WWW.Workflow._workflowid_.Flow"/>). These fields are intentionally omitted
    /// from the form-driven detail view rendered by <see cref="ObjectItemDetailFragment"/> so
    /// the status lives only in this section.
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(1)]
    [Cache]
    public sealed class ObjectPropertyWorkflowCardFragment : FragmentControlPanel
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
        /// attached to a workflow-type field and to walk its state machine.</param>
        /// <param name="valueManager">The value manager used to read the object's current
        /// field values.</param>
        public ObjectPropertyWorkflowCardFragment
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
        /// Renders the workflow status section for the current object. Returns <c>null</c>
        /// when the fragment's render conditions exclude it, when no object can be resolved
        /// from the request, or when the object's class has no workflow-backed fields.
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

            // the status is the one property of the reference zone that has a color of its own,
            // and it is the property a reader looks for first. lending that color to the label
            // and the guide line makes the section findable by color alone in a column of
            // otherwise identical grey labels
            var accent = ResolveAccent(@object, workflowFields);

            var section = new ControlSection("object-property-workflow-section")
            {
                Header = _ => "kleenestar.core:object.property.workflow.header",
                HeaderIcon = _ => new IconTrafficLight(),
                Color = accent is null ? null : _ => new PropertyColorText(accent),
                Layout = _ => TypeLayoutSection.Rule
            };

            var rendered = false;

            foreach (var field in workflowFields)
            {
                var block = BuildFieldBlock(@object, field);
                if (block is not null)
                {
                    section.Add(block);
                    rendered = true;
                }
            }

            return rendered ? section.Render(renderContext, visualTree) : null;
        }

        /// <summary>
        /// Resolves the accent of the section: the category color of the first workflow field
        /// whose status can be resolved.
        /// </summary>
        /// <remarks>
        /// A class may model more than one workflow field, but only one of them can color the
        /// section - so the first one that has a status wins, which is the one the form declared
        /// first and therefore the one the class treats as its primary lifecycle. A class whose
        /// object has not entered any workflow yet gets no accent rather than an arbitrary one.
        /// </remarks>
        /// <param name="object">The object whose status is displayed.</param>
        /// <param name="fields">The workflow-backed fields of the class, in declaration order.</param>
        /// <returns>The category color, or <c>null</c> when no status resolves to one.</returns>
        private string ResolveAccent(Model.Entities.Object @object, IEnumerable<Field> fields)
        {
            foreach (var field in fields)
            {
                var workflow = _workflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

                if (workflow is null)
                {
                    continue;
                }

                var value = _valueManager.GetValue(@object.Id, field.Id);
                var color = _workflowManager.ResolveStatus(workflow, value?.Data)?.Category?.Color;

                if (!string.IsNullOrWhiteSpace(color))
                {
                    return color;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the labelled split-button block for a single workflow-backed field, or
        /// <c>null</c> when the attached workflow can no longer be resolved.
        /// </summary>
        /// <param name="object">The object whose status is displayed.</param>
        /// <param name="field">The workflow-type field being rendered.</param>
        /// <returns>The control hosting the field label and split button, or <c>null</c>.</returns>
        private IControl BuildFieldBlock(Model.Entities.Object @object, Field field)
        {
            // the state machine is needed in full here - the states to offer come from the
            // transitions - so the structural load is used rather than the shallow header read
            var workflow = _workflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

            if (workflow is null)
            {
                return null;
            }

            var value = _valueManager.GetValue(@object.Id, field.Id);
            var current = _workflowManager.ResolveStatus(workflow, value?.Data);
            var currentLabel = current?.Name
                ?? (string.IsNullOrWhiteSpace(value?.Data) ? null : value.Data);

            var split = new ControlSplitButton("object-workflow-" + field.Id.ToString("N"))
            {
                Text = ctx => currentLabel
                    ?? WebExpress.WebCore.Internationalization.I18N.Translate(ctx, "kleenestar.core:object.property.workflow.notset.label"),
                Icon = _ => ResolveIcon(current)
            };

            ApplyCategoryColor(split, current);
            AddTargetItems(split, @object, field, workflow, current);

            split.AddDivider();

            split.Add(new ControlSplitButtonItemLink("object-workflow-show-" + field.Id.ToString("N"))
            {
                Text = _ => "kleenestar.core:object.property.workflow.show.label",
                Icon = _ => new IconWorkflow(),
                PrimaryAction = ctx => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Workflow._workflowid_.Flow>()?
                        .BindParameters(new WorkflowIdParameter(workflow.Id))
                        .BindParameters(ctx.Request),
                    TypeModalSize.ExtraLarge
                )
            });

            var panel = new ControlPanel("object-workflow-field-" + field.Id.ToString("N"))
            {
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two)
            };

            panel.Add(new ControlText("object-workflow-label-" + field.Id.ToString("N"))
            {
                Text = _ => field.Name,
                Format = _ => TypeFormatText.Small
            });
            panel.Add(split);

            return panel;
        }

        /// <summary>
        /// Adds one dropdown entry per state the object may be moved to, each pointing at the
        /// transition endpoint. When the state machine offers no way out of the current state
        /// - a terminal state, or a workflow without transitions - a single disabled entry
        /// says so rather than leaving the dropdown looking broken.
        /// </summary>
        /// <param name="split">The split button being filled.</param>
        /// <param name="object">The object whose state would change.</param>
        /// <param name="field">The workflow-backed field carrying the state.</param>
        /// <param name="workflow">The workflow, loaded with its structure.</param>
        /// <param name="current">The state the object is in, or <c>null</c>.</param>
        private void AddTargetItems(ControlSplitButton split, Model.Entities.Object @object, Field field, Workflow workflow, Status current)
        {
            // what this person may move this object to, not what the state machine allows: a
            // transition whose guards refuse is not offered, so the refusal never arrives after
            // the click
            var targets = _workflowManager
                .GetOfferedStatuses(workflow, current, @object, field, CoreHub.SessionManager.GetCurrentIdentityId(null))
                .ToList();

            if (targets.Count == 0)
            {
                split.Add(new ControlSplitButtonItemLink("object-workflow-notarget-" + field.Id.ToString("N"))
                {
                    Text = _ => "kleenestar.core:object.property.workflow.target.none",
                    Active = _ => TypeActive.Disabled
                });

                return;
            }

            foreach (var status in targets)
            {
                // the transition is only looked up for its label; the endpoint resolves the
                // state change itself, so a target reachable through several transitions still
                // appears once
                var transition = (workflow.Transitions ?? [])
                    .FirstOrDefault(t => t.State == TransitionState.Active
                        && current is not null
                        && t.SourceId == current.Id
                        && t.TargetId == status.Id);

                split.Add(new ControlSplitButtonItemLink("object-workflow-status-" + status.Id.ToString("N"))
                {
                    Text = _ => status.Name,
                    Icon = _ => ResolveIcon(status),
                    Tooltip = _ => transition?.Name,
                    TextColor = _ => new PropertyColorText(status.Category?.Color),
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Transitions._objectkey_.Index>()?
                        .BindParameters(new ObjectKeyParameter(@object.Key))
                        .Add(new UriQuery(FieldIdParameter.Key, field.Id.ToString()))
                        .Add(new UriQuery(WorkflowStateIdParameter.Key, status.Id.ToString()))
                });
            }
        }

        /// <summary>
        /// Paints the split button in the color of the current state's category, so the section
        /// reads at a glance the same way the workflow designer canvas and the board columns
        /// do. Falls back to the system primary color when the state is unknown or its
        /// category carries no color.
        /// </summary>
        /// <param name="split">The split button to color.</param>
        /// <param name="current">The state the object is in, or <c>null</c>.</param>
        private static void ApplyCategoryColor(ControlSplitButton split, Status current)
        {
            var color = current?.Category?.Color;

            if (string.IsNullOrWhiteSpace(color))
            {
                split.BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);

                return;
            }

            split.BackgroundColor = _ => new PropertyColorButton(color);
            split.TextColor = _ => new PropertyColorText(Contrast(color));
        }

        /// <summary>
        /// Returns the glyph standing for a state, chosen from the category the state belongs
        /// to, so the section still separates "not started" from "running", "waiting" and
        /// "finished" for a reader who does not go by color alone.
        /// </summary>
        /// <remarks>
        /// The state's own <see cref="Status.Icon"/> is deliberately not used here. It is an
        /// image icon, and an image icon carries its own colors — a full-bleed tile inside a
        /// button that is already painted in the category color reads as a colored square
        /// rather than as a symbol. A light-theme glyph is a CSS mask instead, so it takes the
        /// surrounding text color and scales with the font, which is what this control needs.
        /// The state images stay in use where they have room to read on their own, such as the
        /// nodes of the workflow graph.
        /// </remarks>
        /// <param name="status">The state, or <c>null</c> when the object carries none.</param>
        /// <returns>The icon to render.</returns>
        private static IIcon ResolveIcon(Status status)
        {
            return Normalize(status?.Category?.Name) switch
            {
                "inprogress" => new IconPlay(),
                "waiting" => new IconPause(),
                "done" => new IconCircleCheck(),
                _ => new IconStatus()
            };
        }

        /// <summary>
        /// Reduces a string to its lower-cased alphanumeric characters so a category name can
        /// be compared regardless of how it is spaced or cased.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The normalized string.</returns>
        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Picks the foreground color that stays readable on the supplied background: white on
        /// a dark category color, near-black on a light one. The decision follows the
        /// perceived brightness of the color rather than its raw average, so the mid-range
        /// category colors (amber, cyan) land on the dark foreground they need.
        /// </summary>
        /// <param name="color">The background color as a <c>#rgb</c> or <c>#rrggbb</c> literal.</param>
        /// <returns>The foreground color literal.</returns>
        private static string Contrast(string color)
        {
            var hex = (color ?? string.Empty).TrimStart('#');

            if (hex.Length == 3)
            {
                hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
            }

            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                // an unparsable color is left to the browser's own contrast handling
                return "#ffffff";
            }

            var brightness = (((rgb >> 16) & 0xFF) * 299 + ((rgb >> 8) & 0xFF) * 587 + (rgb & 0xFF) * 114) / 1000;

            return brightness > 150 ? "#212529" : "#ffffff";
        }
    }
}
