using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebRestApi;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The planning of an object: estimated, logged and left, the milestone it is due with, how
    /// far it is traced, and the ways to log work, plan it and follow its trace.
    /// </summary>
    /// <remarks>
    /// The figures are totals every reader of the object sees; who logged what is on the log
    /// page, for the author and the administrators. Logging is offered only where the
    /// workspace tracks time.
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(4)]
    [Cache]
    public class ObjectPropertyPlanningCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager resolving the URL-bound object key.</param>
        public ObjectPropertyPlanningCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;

            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three);
        }

        /// <summary>
        /// Renders the card.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var @object = _objectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>());

            if (@object is null)
            {
                return null;
            }

            var estimate = CoreHub.PlanningManager.GetEstimate(@object.Id);
            var tracking = CoreHub.PlanningManager.IsTimeTracking(@object.WorkspaceId);
            var milestones = CoreHub.PlanningManager.GetMilestones(@object.WorkspaceId);
            var trace = Traceability.Trace(@object.Id, 2, 50);
            var traced = trace?.Nodes.Count ?? 0;
            var planned = estimate.Original is not null || estimate.Remaining is not null || estimate.Logged > 0 || estimate.MilestoneId is not null;

            // an object nobody plans, in a workspace without time tracking or milestones and
            // without relations, has nothing to show here
            if (!planned && !tracking && milestones.Count == 0 && traced == 0)
            {
                return null;
            }

            var mayWrite = ContentAuthorization.MayWrite(@object, renderContext.Request);

            Clear();

            Add(new ControlText() { Text = _ => "kleenestar.core:planning.card.label", TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });

            if (planned || tracking)
            {
                var figures = string.Format(Translate("kleenestar.core:planning.card.figures"), Durations.Format(estimate.Original), Durations.Format(estimate.Logged), Durations.Format(estimate.Remaining));
                var total = estimate.Logged + (estimate.Remaining ?? 0);
                var percent = total == 0 ? 0u : (uint)Math.Round(100.0 * estimate.Logged / total);

                Add(new ControlText() { Text = _ => figures, Format = _ => TypeFormatText.Small });

                if (total > 0)
                {
                    // spent beyond what was planned shows in the bar's colour, not only in a number
                    var over = estimate.Original is int original && estimate.Logged + (estimate.Remaining ?? 0) > original;

                    Add(new ControlProgress("object-planning")
                    {
                        Value = _ => percent,
                        Color = _ => new PropertyColorProgress(over ? TypeColorProgress.Warning : TypeColorProgress.Primary),
                        Text = _ => percent + " %"
                    });
                }
            }

            if (estimate.MilestoneId is Guid milestoneId && milestones.FirstOrDefault(x => x.Id == milestoneId) is { } milestone)
            {
                var text = string.Format(Translate("kleenestar.core:planning.card.milestone"), milestone.Name, milestone.Due?.ToLocalTime().ToString("d", renderContext.Request.Culture) ?? "-");

                Add(new ControlText() { Text = _ => text, Format = _ => TypeFormatText.Small });
            }

            if (tracking && mayWrite)
            {
                var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Work.Log>()?.Add(new UriQuery("object", @object.Key));

                Add(new ControlLink() { Text = _ => "kleenestar.core:planning.card.log", Uri = _ => uri });
            }

            if (mayWrite)
            {
                var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Work.Estimate>()?.Add(new UriQuery("object", @object.Key));

                Add(new ControlLink() { Text = _ => "kleenestar.core:planning.card.plan", Uri = _ => uri });
            }

            if (traced > 0)
            {
                var text = string.Format(Translate("kleenestar.core:planning.card.trace"), trace.Nodes.Count(x => x.Upstream), trace.Nodes.Count(x => !x.Upstream));
                var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Work.Trace>()?.Add(new UriQuery("object", @object.Key));

                Add(new ControlLink() { Text = _ => text, Uri = _ => uri });
            }

            return base.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The planning of a document or a blog post, below its content.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Order(91)]
    [Cache]
    public sealed class ObjectContentPlanningCardFragment : ObjectPropertyPlanningCardFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager resolving the URL-bound object key.</param>
        public ObjectContentPlanningCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext, objectManager)
        {
        }
    }
}
