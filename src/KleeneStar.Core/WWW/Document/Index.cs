using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Document
{
    /// <summary>
    /// Hidden anchor of the <c>/document</c> route segment: the segment carries the
    /// per-document detail pages under <c>/document/{objectkey}</c>, but is not itself a
    /// navigable page, so a bare <c>/document</c> request redirects to the application
    /// home. Mirrors the anchors of the other detail routes (<see cref="Issue.Index"/>,
    /// <see cref="Blog.Index"/>).
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
            throw new RedirectException
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Index>()
            );
        }
    }
}
