using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Insight._insightid_
{
    /// <summary>
    /// Provides functionality for displaying a single insight.
    /// </summary>
    [WebIcon<IconChartPie>]
    [InsightIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Insight>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var insightParameter = renderContext.Request.GetParameter<InsightIdParameter>();
            var insight = CoreHub.InsightManager.GetInsight(insightParameter);

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new UriPathSegmentConstant("insights")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Insights.Index>()
                })
                .Concat(new InsightIdUriPathSegmentVariable<InsightIdParameter>()
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;

            visualTree.Title = insight?.Name;
            visualTree.Content.MainPanel.Headline.Title = insight?.Name;
        }
    }
}
