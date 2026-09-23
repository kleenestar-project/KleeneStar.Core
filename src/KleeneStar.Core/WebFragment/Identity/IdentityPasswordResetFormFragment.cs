using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Identity
{
    /// <summary>
    /// The dialog that issues a one-time link setting the password of an internal account.
    /// </summary>
    /// <remarks>
    /// The form carries nothing but the account; submitting it issues the link, which the
    /// dialog then shows - the only time it can be read - for the administrator to hand over.
    /// An external account gets a sentence instead of the form: its password is its source's.
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Password>]
    [Cache]
    public sealed class IdentityPasswordResetFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the hidden input carrying the account.
        /// </summary>
        public ControlFormItemInputHidden IdentityId { get; } = new()
        {
            Name = _ => nameof(PasswordReset.IdentityId)
        };

        /// <summary>
        /// Gets the text explaining what the link does.
        /// </summary>
        public ControlFormItemStaticText Explanation { get; } = new()
        {
            Text = _ => "kleenestar.core:setting.identity.password.description"
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IdentityPasswordResetFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(IdentityId);
            Add(Explanation);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Identities.PasswordResets>();
        }

        /// <summary>
        /// Renders the form for an internal account, and a notice for anything else.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var account = CoreHub.IdentityManager.GetIdentity(renderContext.Request.GetParameter<IdentityIdParameter>());

            var refusal = account is null
                ? "kleenestar.core:setting.identity.password.validation.unknown"
                : !AccountAuthorization.IsAdministrator(renderContext.Request)
                    ? "kleenestar.core:setting.identity.password.validation.forbidden"
                    : !AuthenticationSourceCatalog.ManagesPassword(account)
                        ? "kleenestar.core:setting.identity.password.validation.external"
                        : null;

            if (refusal is not null)
            {
                return new ControlAlert("identity-password-refused")
                {
                    Text = _ => I18N.Translate(renderContext, refusal),
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Info),
                    Dismissibility = _ => TypeDismissibilityAlert.None
                }
                    .Render(renderContext, visualTree);
            }

            renderContext.SetValue(IdentityId, new ControlFormInputValueString(account.Id.ToString()));

            return base.Render(renderContext, visualTree);
        }
    }
}
