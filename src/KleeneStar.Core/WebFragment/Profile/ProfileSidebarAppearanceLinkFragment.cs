using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// Sidebar link to the appearance page (theme, accent, density, font scale).
    /// </summary>
    [Section<SectionSidebarPreferences>]
    [Order(3)]
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
    public sealed class ProfileSidebarAppearanceLinkFragment : FragmentControlSidebarItemLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Appearance>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ProfileSidebarAppearanceLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconPalette();
            Text = _ => "kleenestar.core:profile.appearance.title";
            Uri = _ => _uri;
            Active = renderContext => ProfileSidebarUriHelper.IsActive(renderContext, _uri)
                ? TypeActive.Active
                : TypeActive.None;
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
            return base.Render(renderContext, visualTree);
        }
    }
}
