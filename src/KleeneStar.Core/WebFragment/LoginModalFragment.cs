using WebExpress.WebApp.WebCondition;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// The login dialog that <see cref="LoginLinkFragment"/> (the Login entry of the avatar
    /// menu) opens, so signing in happens on top of the page the user is on.
    /// </summary>
    /// <remarks>
    /// The dialog is the framework's <see cref="ControlDataModalLogin"/>: the login dialog of
    /// WebUI framing the REST login, which submits the credentials to the
    /// <see cref="WWW.Api._1_.Session"/> endpoint - the one the full-page login at
    /// <see cref="WWW.Session.Index"/> uses as well - and reloads the page once the session
    /// cookie is set. It used to be a remote-page modal that fetched that login page on every
    /// click and lifted its form out; the dialog is now rendered with the page, so it opens
    /// without a round trip and the link names only its id.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Condition<ConditionLogout>]
    [Cache]
    public sealed class LoginModalFragment : ControlDataModalLogin, IFragmentControl<ControlDataModalLogin>
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class with the well-known
        /// <c>modal-login</c> id, so the avatar Login link can target it.
        /// The id <c>modal-form</c> is reserved for <see cref="ModalFormFragment"/>
        /// (REST add/edit/clone/delete forms); sharing it would race with the
        /// form modal when the user is logged out and hide the form modal body.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public LoginModalFragment(IFragmentContext fragmentContext)
            : base("modal-login")
        {
            FragmentContext = fragmentContext;
            Header = _ => "webexpress.webapp:login.label";

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Session>();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control, or null when the user is signed in.
        /// </returns>
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
