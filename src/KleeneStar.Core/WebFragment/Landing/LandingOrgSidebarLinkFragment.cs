using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// Links to the workspaces of the organization.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Scope<global::KleeneStar.Core.WWW.Mine.Index>]
    [Scope<global::KleeneStar.Core.WWW.Shared.Index>]
    [Scope<global::KleeneStar.Core.WWW.Watched.Index>]
    [Order(30)]
    [Cache]
    public sealed class LandingOrgSidebarLinkFragment : LandingSidebarLinkFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        public LandingOrgSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Gets the route the link points at.
        /// </summary>
        protected override IUri Target => CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces.Index>();

        /// <summary>
        /// Gets the resource key of the link label.
        /// </summary>
        protected override string Label => "kleenestar.core:landing.paths.org.label";

        /// <summary>
        /// Gets the icon of the link.
        /// </summary>
        protected override IIcon Symbol => new IconBuilding();
    }
}
