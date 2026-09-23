using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebManager;
using System;
using System.Globalization;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The password card of the security page: the form that changes the caller's password,
    /// or - for an account whose credentials an external source owns - where to change it
    /// instead.
    /// </summary>
    /// <remarks>
    /// The two are one fragment because the question is one: whose is this password? An
    /// internal account's is this installation's, and the owner changes it here by proving the
    /// current one (<see cref="WWW.Api._1_.Profile.Password"/>). An external account's is its
    /// source's, and a form here could only ever be refused.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Security>]
    [Cache]
    public sealed class ProfilePasswordFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input for the current password.
        /// </summary>
        public ControlFormItemInputPassword CurrentPassword { get; } = new()
        {
            Name = _ => WWW.Api._1_.Profile.Password.CurrentField,
            Label = _ => "kleenestar.core:profile.security.password.current.label",
            Help = LastChanged,
            Required = _ => true
        };

        /// <summary>
        /// Gets the input for the new password.
        /// </summary>
        public ControlFormItemInputPassword NewPassword { get; } = new()
        {
            Name = _ => WWW.Api._1_.Profile.Password.NewField,
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
            Name = _ => WWW.Api._1_.Profile.Password.ConfirmField,
            Label = _ => "kleenestar.core:profile.security.password.confirm.label",
            Required = _ => true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfilePasswordFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(CurrentPassword);
            Add(NewPassword);
            Add(ConfirmPassword);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Profile.Password>();

            // the endpoint changes the caller's password whatever id it is sent; the id is only
            // what the edit form needs to address a record at all
            ItemId = renderContext =>
            {
                var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

                return identityId == Guid.Empty ? null : identityId.ToString();
            };
        }

        /// <summary>
        /// Renders the form for an internal account, the pointer to the source for an external
        /// one, and nothing for a caller who is not signed in.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var account = CoreHub.IdentityManager.GetCurrentIdentity(renderContext?.Request);

            if (account is null)
            {
                return null;
            }

            if (!AuthenticationSourceCatalog.ManagesPassword(account))
            {
                var source = AuthenticationSourceCatalog.Resolve(account);
                var name = source is null ? account.AuthenticationSource : I18N.Translate(renderContext, source.Name);

                return new ControlAlert("profile-password-external")
                {
                    Head = _ => I18N.Translate(renderContext, "kleenestar.core:profile.security.password.label"),
                    Text = _ => I18N.Translate(renderContext.Request, "kleenestar.core:profile.security.password.external", name),
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Info),
                    Dismissibility = _ => TypeDismissibilityAlert.None
                }
                    .Render(renderContext, visualTree);
            }

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Says when the caller's password was last set - the one thing the owner can check the
        /// account by without reading the log.
        /// </summary>
        /// <remarks>
        /// Read per request: the fragment is cached and shared, so nothing about the caller may
        /// be stored on it.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The help text, or <see langword="null"/> when the password was never set.</returns>
        private static string LastChanged(IRenderControlContext renderContext)
        {
            var changed = CoreHub.IdentityManager.GetCurrentIdentity(renderContext?.Request)?.PasswordChanged;

            return changed.HasValue
                ? I18N.Translate
                (
                    renderContext.Request,
                    "kleenestar.core:profile.security.password.lastchanged",
                    changed.Value.ToLocalTime().ToString("g", renderContext.Request?.Culture ?? CultureInfo.CurrentCulture)
                )
                : null;
        }
    }
}
