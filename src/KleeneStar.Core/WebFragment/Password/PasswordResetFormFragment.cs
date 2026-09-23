using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;
using ResetEndpoint = KleeneStar.Core.WWW.Api._1_.Password.Reset;

namespace KleeneStar.Core.WebFragment.Password
{
    /// <summary>
    /// The form a reset link opens: the new password, twice, and the link's secret carried
    /// along in a hidden field.
    /// </summary>
    /// <remarks>
    /// A link that sets nothing - unknown, spent, expired, or for an account that is no longer
    /// internal - is answered before the form is drawn, with a sentence instead of a form that
    /// could only be refused.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.SetPassword.Index>]
    [Cache]
    public sealed class PasswordResetFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the hidden input carrying the secret from the link.
        /// </summary>
        public ControlFormItemInputHidden Token { get; } = new()
        {
            Name = _ => ResetEndpoint.TokenField
        };

        /// <summary>
        /// Gets the input for the new password.
        /// </summary>
        public ControlFormItemInputPassword NewPassword { get; } = new()
        {
            Name = _ => ResetEndpoint.NewField,
            Label = _ => "kleenestar.core:profile.security.password.new.label",
            Help = _ => "kleenestar.core:password.rules",
            MinLength = _ => ICredentialManager.MinimumLength,
            MaxLength = _ => ICredentialManager.MaximumLength,
            Required = _ => true
        };

        /// <summary>
        /// Gets the input that repeats the new password.
        /// </summary>
        public ControlFormItemInputPassword ConfirmPassword { get; } = new()
        {
            Name = _ => ResetEndpoint.ConfirmField,
            Label = _ => "kleenestar.core:profile.security.password.confirm.label",
            Required = _ => true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PasswordResetFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Token);
            Add(NewPassword);
            Add(ConfirmPassword);

            this.DataService<ResetEndpoint>();
        }

        /// <summary>
        /// Renders the form for a link that sets a password, and a notice for one that does not.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var secret = renderContext?.Request?.GetParameter("token")?.Value;

            if (CoreHub.CredentialManager.GetPasswordReset(secret) is null)
            {
                return new ControlAlert("password-reset-invalid")
                {
                    Head = _ => I18N.Translate(renderContext, "kleenestar.core:password.reset.title"),
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:password.reset.validation.link"),
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Warning),
                    Dismissibility = _ => TypeDismissibilityAlert.None
                }
                    .Render(renderContext, visualTree);
            }

            renderContext.SetValue(Token, new ControlFormInputValueString(secret));

            return base.Render(renderContext, visualTree);
        }
    }
}
