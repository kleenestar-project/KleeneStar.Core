using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Profile link in the user avatar dropdown. Visible only when a user is signed in;
    /// targets the profile settings page (Index of WWW/Settings/Profile).
    /// </summary>
    [Section<SectionAppAvatarPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Cache]
    public sealed class ProfileLinkFragment : FragmentControlDropdownItemLink
    {
        private readonly IComponentHub _componentHub;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub used to manage components.</param>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ProfileLinkFragment(IComponentHub componentHub, IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            _componentHub = componentHub;
            Text = _ => "kleenestar.core:profile.dropdown.label";
            Icon = _ => new IconCircleUser();
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Index>();
        }

        /// <summary>
        /// Convert the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
