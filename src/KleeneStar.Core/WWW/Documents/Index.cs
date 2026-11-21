using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Documents
{
    /// <summary>
    /// Anchors the <c>/documents</c> route segment as the parent of the per-workspace
    /// document overview, so the overview inherits the breadcrumb chain of the sitemap
    /// (mirroring the retired objects route). The segment itself is hidden and renders
    /// no content of its own.
    /// </summary>
    [WebIcon<IconFileLines>]
    [SegmentHidden]
    [Scope<IScopeGeneral>]
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
        }
    }
}
