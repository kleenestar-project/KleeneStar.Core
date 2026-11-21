using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Identity
{
    /// <summary>
    /// Answers a caller who does not administer the accounts before an identity dialog is drawn.
    /// </summary>
    /// <remarks>
    /// The endpoint behind the dialogs refuses such a caller (<see cref="AccountAuthorization"/>),
    /// and a form that is drawn anyway loads nothing and reports a bare failed request. The
    /// dialog says why instead - most often the caller is simply not signed in.
    /// </remarks>
    internal static class IdentityFormGuard
    {
        /// <summary>
        /// Renders the refusal for a caller who does not administer the accounts.
        /// </summary>
        /// <param name="renderContext">The context in which the dialog is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The refusal, or <see langword="null"/> when the caller may use the dialog.</returns>
        public static IHtmlNode Refuse(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (AccountAuthorization.IsAdministrator(renderContext?.Request))
            {
                return null;
            }

            var signedIn = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request) != System.Guid.Empty;
            var key = signedIn
                ? "kleenestar.core:setting.identity.validation.forbidden"
                : "kleenestar.core:setting.identity.validation.signedout";

            return new ControlAlert("identity-form-refused")
            {
                Text = _ => I18N.Translate(renderContext, key),
                BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Info),
                Dismissibility = _ => TypeDismissibilityAlert.None
            }
                .Render(renderContext, visualTree);
        }
    }
}
