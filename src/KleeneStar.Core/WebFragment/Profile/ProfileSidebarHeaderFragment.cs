using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// Sidebar header for the profile area — shown on every profile page.
    /// </summary>
    [Section<SectionSidebarPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Index>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Account>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tenant>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Appearance>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Notifications>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Security>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Sessions.Index>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tokens.Index>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Integrations>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Data>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Delete>]
    [Cache]
    public sealed class ProfileSidebarHeaderFragment : FragmentControlSidebarItemHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context associated with the fragment.</param>
        public ProfileSidebarHeaderFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Convert the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the fragment is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree used for rendering the fragment.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered fragments. Can be null if no nodes are present.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree, I18N.Translate(renderContext, "kleenestar.core:profile.sidebar.header"));
        }
    }
}
