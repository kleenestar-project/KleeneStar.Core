using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    // The entity type names collide with the KleeneStar.Core.WWW.* namespace segments of
    // the same name; alias them inside the namespace block so Status resolves to the model
    // entity here (see also the Calendar namespace-collision note).
    using Status = KleeneStar.Model.Entities.Status;

    /// <summary>
    /// The service-level section of the reference zone, showing every SLA policy attached to the
    /// current object's class as running agreements on
    /// <see cref="WWW.Issue._objectkey_.Index"/>.
    /// </summary>
    /// <remarks>
    /// Each active <see cref="SlaPolicy"/> of the class becomes one <see cref="ControlSla"/>
    /// group carrying its name, its severity bucket and the summary of how its targets are
    /// doing; each <see cref="SlaTarget"/> inside it becomes one
    /// <see cref="ControlDataSla"/> tile - a coloured status, a meter of the consumed budget
    /// and the time left until the deadline.
    /// <para>
    /// The tiles are rendered complete: the clock <see cref="SlaClock.Derive"/> builds from
    /// the object is evaluated server side and seeded into the markup, so the section is correct
    /// in the first paint and stays readable without JavaScript. The client then counts on
    /// its own and re-reads the state from
    /// <see cref="WWW.Api._1_.SlaClocks._objectkey_.Index"/> once a minute, which is what
    /// keeps a tile in step with a colleague who moved the ticket in another tab.
    /// </para>
    /// <para>
    /// The tiles carry no actions. Pausing or settling an agreement by hand would have to be
    /// written somewhere, and the clock is derived from the object's workflow status rather
    /// than stored - so the way to stop it is to move the ticket into one of the policy's
    /// <see cref="SlaPolicy.PauseOn"/> statuses.
    /// </para>
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class IssueSlaCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// The interval in seconds at which a tile re-reads its state from the endpoint. The
        /// countdown itself runs in the client, so the poll only has to notice the changes the
        /// client cannot know about - a status change that paused or settled the agreement.
        /// </summary>
        private const int RefreshIntervalSeconds = 60;

        private readonly IObjectManager _objectManager;
        private readonly ISlaManager _slaManager;
        private readonly IFieldManager _fieldManager;
        private readonly IValueManager _valueManager;
        private readonly IWorkflowManager _workflowManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        /// <param name="slaManager">The SLA manager used to load the policies attached
        /// to the resolved object's class.</param>
        /// <param name="fieldManager">The field manager used to find the workflow-backed
        /// fields the object's status is read from.</param>
        /// <param name="valueManager">The value manager used to read those field values.</param>
        /// <param name="workflowManager">The workflow manager used to resolve a value against
        /// the states of its workflow.</param>
        public IssueSlaCardFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            ISlaManager slaManager,
            IFieldManager fieldManager,
            IValueManager valueManager,
            IWorkflowManager workflowManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _slaManager = slaManager;
            _fieldManager = fieldManager;
            _valueManager = valueManager;
            _workflowManager = workflowManager;
        }

        /// <summary>
        /// Renders the SLA section for the current object.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>An HTML node, or <c>null</c> when the fragment's render conditions
        /// exclude it or when no object can be resolved from the request.</returns>
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

            var section = new ControlSection("object-sla-section")
            {
                Header = _ => "kleenestar.core:object.sla.card.header",
                HeaderIcon = _ => new IconStopwatch(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var policies = _slaManager
                .GetSlas(@object.ClassId)
                .Where(p => p.State == SlaPolicyState.Active)
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.Name)
                .ToList();

            // a class without an agreement has no section: most classes never get one, and a
            // card announcing its own absence on every ticket of them is noise, not information.
            // A policy that names no target stays reported below - that is a configuration
            // somebody started and did not finish, which is worth a line
            if (policies.Count == 0)
            {
                return null;
            }

            // the status is what decides whether a clock runs, is stopped or is settled, so it
            // is resolved once for the whole section rather than per target
            var status = SlaClock.ResolveStatus(@object, _fieldManager, _valueManager, _workflowManager);
            var moment = DateTime.Now;
            var rendered = false;

            foreach (var policy in policies)
            {
                var group = BuildPolicyGroup(@object, policy, status, moment);

                if (group is not null)
                {
                    section.Add(group);
                    rendered = true;
                }
            }

            if (!rendered)
            {
                section.Add(EmptyState("object-sla-notargets", "kleenestar.core:object.sla.card.notargets"));
            }

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the group framing the agreements of one policy, or <c>null</c> when the
        /// policy defines no target - a policy without a target promises nothing, so there is
        /// no clock to show for it.
        /// </summary>
        /// <param name="object">The object the agreements are measured against.</param>
        /// <param name="policy">The policy being rendered.</param>
        /// <param name="status">The workflow status the object carries, or <c>null</c>.</param>
        /// <param name="moment">The moment the clocks are read at.</param>
        /// <returns>The group, or <c>null</c>.</returns>
        private static IControl BuildPolicyGroup(Model.Entities.Object @object, SlaPolicy policy, Status status, DateTime moment)
        {
            var targets = (policy.Targets ?? []).OrderBy(t => t.Kind).ThenBy(t => t.Name).ToList();

            if (targets.Count == 0)
            {
                return null;
            }

            var group = new ControlSla("object-sla-policy-" + policy.Id.ToString("N"))
            {
                Label = _ => policy.Name,
                Description = ctx => I18N.Translate(ctx, policy.Priority.TranslationKey())
            };

            group.Add(targets.Select(t => BuildTargetTile(@object, policy, t, status, moment)).ToArray());

            return group;
        }

        /// <summary>
        /// Builds the tile of a single target: seeded with the clock derived on the server and
        /// wired to the endpoint it re-reads that clock from.
        /// </summary>
        /// <param name="object">The object the agreement is measured against.</param>
        /// <param name="policy">The policy the target belongs to.</param>
        /// <param name="target">The target being rendered.</param>
        /// <param name="status">The workflow status the object carries, or <c>null</c>.</param>
        /// <param name="moment">The moment the clock is read at.</param>
        /// <returns>The tile.</returns>
        private static ControlDataSla BuildTargetTile(Model.Entities.Object @object, SlaPolicy policy, SlaTarget target, Status status, DateTime moment)
        {
            var tile = new ControlDataSla("object-sla-target-" + target.Id.ToString("N"))
            {
                Label = ctx => string.IsNullOrWhiteSpace(target.Name)
                    ? I18N.Translate(ctx, target.Kind.TranslationKey())
                    : target.Name,
                Description = ctx => $"{target.TargetValue} {I18N.Translate(ctx, target.Unit.TranslationKey())}",
                // there is no per-object clock to write a pause or a settlement to, so the
                // tile reports the agreement instead of offering to change it
                ShowActions = _ => false,
                RefreshInterval = _ => RefreshIntervalSeconds
            };

            tile.Bind(SlaClock.Derive(@object, policy, target, status, moment));
            tile.DataService<global::KleeneStar.Core.WWW.Api._1_.SlaClocks._objectkey_.Index>
            (
                descriptor => descriptor.WithBaseUri(AddTarget(descriptor.BaseUri, target.Id))
            );

            return tile;
        }

        /// <summary>
        /// Adds the target id to the endpoint address, which is what makes the tiles of one
        /// object address one agreement each. The endpoint itself is resolved through the
        /// sitemap by the data service, so only the query is spelled here.
        /// </summary>
        /// <param name="baseUri">The resolved endpoint address.</param>
        /// <param name="targetId">The id of the target the tile shows.</param>
        /// <returns>The address carrying the target id.</returns>
        private static string AddTarget(string baseUri, Guid targetId)
        {
            if (string.IsNullOrEmpty(baseUri))
            {
                return baseUri;
            }

            var separator = baseUri.Contains('?') ? '&' : '?';

            return $"{baseUri}{separator}{SlaTargetIdParameter.Key}={targetId}";
        }

        /// <summary>
        /// Builds the line the section shows in place of the agreements when there are none.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        /// <param name="key">The i18n key of the message.</param>
        /// <returns>The control.</returns>
        private static IControl EmptyState(string id, string key)
        {
            return new ControlText(id)
            {
                Text = _ => key,
                Format = _ => TypeFormatText.Italic
            };
        }
    }

    /// <summary>
    /// Provides translation-key lookups for SLA enums used by the section layout.
    /// </summary>
    internal static class SlaTranslationKeyExtensions
    {
        /// <summary>
        /// Returns the i18n key for an SLA priority bucket.
        /// </summary>
        public static string TranslationKey(this SlaPriority priority)
        {
            return priority switch
            {
                SlaPriority.Low => "kleenestar.core:sla.priority.low.label",
                SlaPriority.Medium => "kleenestar.core:sla.priority.medium.label",
                SlaPriority.High => "kleenestar.core:sla.priority.high.label",
                SlaPriority.Critical => "kleenestar.core:sla.priority.critical.label",
                _ => null
            };
        }

        /// <summary>
        /// Returns the i18n key for an SLA target milestone.
        /// </summary>
        public static string TranslationKey(this SlaTargetKind kind)
        {
            return kind switch
            {
                SlaTargetKind.Response => "kleenestar.core:sla.targetkind.response.label",
                SlaTargetKind.Resolution => "kleenestar.core:sla.targetkind.resolution.label",
                SlaTargetKind.Update => "kleenestar.core:sla.targetkind.update.label",
                SlaTargetKind.Approval => "kleenestar.core:sla.targetkind.approval.label",
                SlaTargetKind.Implementation => "kleenestar.core:sla.targetkind.implementation.label",
                SlaTargetKind.Fulfillment => "kleenestar.core:sla.targetkind.fulfillment.label",
                SlaTargetKind.Custom => "kleenestar.core:sla.targetkind.custom.label",
                _ => null
            };
        }

        /// <summary>
        /// Returns the i18n key for an SLA target unit.
        /// </summary>
        public static string TranslationKey(this SlaTargetUnit unit)
        {
            return unit switch
            {
                SlaTargetUnit.Minutes => "kleenestar.core:sla.targetunit.minutes.label",
                SlaTargetUnit.Hours => "kleenestar.core:sla.targetunit.hours.label",
                SlaTargetUnit.Days => "kleenestar.core:sla.targetunit.days.label",
                SlaTargetUnit.BusinessDays => "kleenestar.core:sla.targetunit.businessdays.label",
                _ => null
            };
        }
    }
}
