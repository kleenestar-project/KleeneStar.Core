using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's dashboard tab - what an insight was before it had tabs. The
    /// board belongs to the insight, so every dashboard tab of an insight shows the same one.
    /// </summary>
    [Section<WebExpress.WebUI.WebSection.SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabDashboardTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabDashboardTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Dashboard)
        {
        }
    }

    /// <summary>
    /// The dashboard of an insight: columns of freely arranged widgets, backed by
    /// <c>/api/1/insights/{insightid}/view</c>, which persists every change of a caller who may
    /// change the insight. The KleeneStar widget types and their texts are inlined into the page
    /// (<see cref="DashboardWidgetScript"/>).
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabDashboardTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabDashboardFragment : FragmentControlDataDashboard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabDashboardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the board is the view of the tab, so it takes the height it is handed and its
            // widgets scroll below a menu bar that stays
            Fill = _ => true;

            ServiceFactory = _ => DataServiceDescriptor.Data(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.View>().ToString());

            EditableColumn = _ => true;
            MovableColumn = _ => true;
            DeletableColumn = _ => true;
            AddableColumn = _ => true;
            AddableWidget = _ => true;
            ConfigurableWidget = _ => true;
        }

        /// <summary>
        /// Renders the board, first injecting the KleeneStar widget registration into the page head.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var script = DashboardWidgetScript.Value;

            if (!string.IsNullOrEmpty(script))
            {
                visualTree.AddHeaderScript(script);
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
