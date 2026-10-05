using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's reports tab: charts computed from the history of the
    /// insight's objects - cumulative flow, velocity, average age, created vs. resolved and
    /// resolution time.
    /// </summary>
    [Section<WebExpress.WebUI.WebSection.SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(6)]
    [Cache]
    public sealed class InsightTabReportsTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabReportsTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Reports)
        {
        }
    }

    /// <summary>
    /// The report surface of the reports tab, loaded and drawn by <c>insightreports.js</c> from
    /// <c>/api/1/insights/{insightid}/reports</c>.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabReportsTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabReportsFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabReportsFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(new InsightReportControl()
            {
                Uri = renderContext => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Reports>()?
                    .BindParameters(renderContext.Request)?
                    .ToString(),
                StorageKey = renderContext => $"kleenestar.insight.report.{renderContext.Request?.GetParameter<InsightIdParameter>()?.Value}"
            });
        }
    }
}
