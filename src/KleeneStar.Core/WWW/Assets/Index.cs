using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Assets
{
    /// <summary>
    /// Anchors the <c>/assets</c> route segment as the parent of the per-workspace asset
    /// overview, so the overview inherits the breadcrumb chain of the sitemap (mirroring
    /// the other kind overviews). The segment itself is hidden and renders no content of
    /// its own. It sits beside the <see cref="Icons.Index"/> icon resource, which owns
    /// the literal <c>/assets/icons</c> sub-route.
    /// </summary>
    [WebIcon<IconCubes>]
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
