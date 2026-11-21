using KleeneStar.Core.WebPermission;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Refuses every page of the core application whose route the caller holds no permission on,
    /// before any of it reaches the browser. Renders nothing itself.
    /// </summary>
    /// <remarks>
    /// WebExpress offers no hook in front of a page, and the framework's own page policies
    /// cannot express a grant on a workspace or class (see <see cref="PageAuthorization"/>). A
    /// fragment standing in every page's primary section is rendered for every page, though, and
    /// a <c>ForbiddenException</c> thrown while the page is rendered ends the request the same way
    /// one thrown from <c>Process</c> does: nothing of the refused page is sent, and the server
    /// answers at the same address with the forbidden page or, for a caller who is not signed in,
    /// the sign-in prompt. <see cref="RouteAuthorization"/> says what each route
    /// demands; this fragment only asks it.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class PageGuardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PageGuardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Refuses the page when the caller holds no permission on its route.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns><see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var pageId = renderContext?.PageContext?.EndpointId?.ToString();

            if (!RouteAuthorization.IsGranted(pageId, renderContext?.Request))
            {
                throw PageAuthorization.Refuse();
            }

            return null;
        }
    }
}
