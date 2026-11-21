using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Blog
{
    /// <summary>
    /// Hidden anchor of the <c>/blog</c> route segment: the segment carries the
    /// per-post detail pages under <c>/blog/{objectkey}</c>, but is not itself a
    /// navigable page, so a bare <c>/blog</c> request redirects to the application home.
    /// Mirrors the anchors of the other detail routes (<see cref="Issue.Index"/>,
    /// <see cref="Document.Index"/>).
    /// </summary>
    [WebIcon<IconBlog>]
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
            throw new RedirectException
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Index>()
            );
        }
    }
}
