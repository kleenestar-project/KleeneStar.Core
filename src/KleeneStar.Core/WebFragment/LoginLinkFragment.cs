using WebExpress.WebApp.WebCondition;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Represents a dropdown menu item that provides a login link for the user within the application's navigation UI.
    /// </summary>
    /// <remarks>
    /// The entry opens the login dialog <see cref="LoginModalFragment"/> renders with the page,
    /// so signing in happens on top of the page the user is on. The dialog is on the page
    /// already, which is why the action names only its id and fetches nothing. The fragment is
    /// cached for performance and is only visible when the user is logged out.
    /// </remarks>
    [Section<SectionAppAvatarSecondary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Condition<ConditionLogout>]
    [Cache]
    public sealed class LoginLinkFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public LoginLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "webexpress.webapp:login.label";
            Icon = _ => new IconRightToBracket();
            PrimaryAction = _ => new ActionModal("modal-login");
        }

        /// <summary>
        /// Convert the control to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
