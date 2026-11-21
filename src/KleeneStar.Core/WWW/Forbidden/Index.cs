using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Forbidden
{
    /// <summary>
    /// The page a refused request lands on: the framework's access-denied card inside the
    /// application's own chrome, so the user can see what was refused and still navigate away.
    /// </summary>
    /// <remarks>
    /// A page in this framework cannot answer a status of its own - its result is whatever the
    /// visual tree renders - so a page that refuses a caller redirects here instead of rendering
    /// half of itself (see <see cref="WebPermission.PageAuthorization"/>). The card is the same
    /// one the framework shows when a page-level policy refuses an identity; only the route and
    /// the chrome around it are this application's.
    /// </remarks>
    [WebIcon<IconBan>]
    [SegmentHidden]
    [Title("webexpress.webapp:forbidden.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : PageWebAppForbidden, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource: renders the access-denied card.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public override void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            base.Process(renderContext, visualTree);
        }
    }
}
