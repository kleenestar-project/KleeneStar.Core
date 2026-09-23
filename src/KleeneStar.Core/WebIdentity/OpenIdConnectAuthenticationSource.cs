using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebSetting;
// the framework carries an identity of its own in a namespace this file imports, so the
// stored one is named explicitly rather than by the name both of them carry
using Identity = KleeneStar.Model.Entities.Identity;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// An external source that authenticates its accounts at an OpenID Connect identity
    /// provider, through WebExpress's authorization-code flow (<c>/api/auth/authorize</c> and
    /// <c>/api/auth/callback</c>).
    /// </summary>
    /// <remarks>
    /// The framework verifies the provider's token; this class decides which stored account it
    /// stands for (<see cref="ICredentialManager.SignInExternal"/>) and answers that account, so
    /// the credential the framework then issues carries the stored id like a password sign-in
    /// does - and the session resolves it without matching any name. A sign-in that stands for
    /// no account is refused.
    /// <para>
    /// A source is configured, not coded: every entry of
    /// <c>Plugins:kleenestar.core:Authentication:OpenIdConnect</c> becomes one (see
    /// <see cref="RegisterConfigured"/>), registered both as an authentication source of the
    /// catalog and as an identity provider of the application.
    /// </para>
    /// </remarks>
    public sealed class OpenIdConnectAuthenticationSource : OpenIdConnectIdentityProvider, IAuthenticationSource
    {
        /// <summary>
        /// The section of the plugin settings the sources are configured in.
        /// </summary>
        public const string Section = "Authentication:OpenIdConnect";

        /// <summary>
        /// Gets the key the accounts of this source carry - the configured provider id.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the name the source is shown under, as configured.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the position of the source in listings: after the internal source.
        /// </summary>
        public int Order => 100;

        /// <summary>
        /// Gets whether the credentials are owned elsewhere: by the identity provider.
        /// </summary>
        public bool IsExternal => true;

        /// <summary>
        /// Gets whether the sign-in form's password is checked here: never - the browser is sent
        /// to the identity provider instead.
        /// </summary>
        public bool AcceptsPassword => false;

        /// <summary>
        /// Gets whether a sign-in that stands for no stored account creates one.
        /// </summary>
        public bool Provisions { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="settings">The configured source.</param>
        public OpenIdConnectAuthenticationSource(OpenIdConnectSourceSettings settings)
            : base(new OpenIdConnectSettings
            {
                ProviderId = IdentitySource.Normalize(settings?.Key),
                Authority = settings?.Authority,
                ClientId = settings?.ClientId,
                ClientSecret = settings?.ClientSecret,
                RedirectUri = settings?.RedirectUri
            })
        {
            Key = IdentitySource.Normalize(settings.Key);
            Name = string.IsNullOrWhiteSpace(settings.Name) ? Key : settings.Name;
            Provisions = settings.Provision;
        }

        /// <summary>
        /// Refuses every password: an account of this source signs in at its identity provider.
        /// </summary>
        /// <param name="account">The stored account.</param>
        /// <param name="password">The offered password.</param>
        /// <returns><see langword="false"/>.</returns>
        public bool VerifyPassword(Identity account, string password)
        {
            return false;
        }

        /// <summary>
        /// Answers the stored account the verified token stands for.
        /// </summary>
        /// <remarks>
        /// The framework's own mapping hashes the issuer and subject into an id no stored
        /// account carries, and maps the provider's roles onto permissions. Neither is wanted
        /// here: the account is the stored one, and what it may do comes from its groups in this
        /// installation, not from roles the provider names.
        /// </remarks>
        /// <param name="token">The verified ID token.</param>
        /// <returns>The stored account, or <see langword="null"/> when the token stands for none.</returns>
        protected override WebExpress.WebCore.WebIdentity.IIdentity MapIdentity(JsonWebToken token)
        {
            if (token is null || string.IsNullOrWhiteSpace(token.Subject))
            {
                return null;
            }

            token.TryGetPayloadValue<string>("preferred_username", out var userName);
            token.TryGetPayloadValue<string>("name", out var name);
            token.TryGetPayloadValue<string>("email", out var email);

            var claims = new ExternalAccountClaims(token.Subject, userName, name, email, EmailVerified(token));

            return CoreHub.CredentialManager.SignInExternal(Key, claims);
        }

        /// <summary>
        /// Reads whether the provider vouches for the e-mail address. Providers write the claim
        /// as a boolean or as a string; anything else is read as "not vouched for".
        /// </summary>
        /// <param name="token">The verified ID token.</param>
        /// <returns><see langword="true"/> when the address is verified.</returns>
        private static bool EmailVerified(JsonWebToken token)
        {
            if (token.TryGetPayloadValue<bool>("email_verified", out var verified))
            {
                return verified;
            }

            return token.TryGetPayloadValue<string>("email_verified", out var text)
                && bool.TryParse(text, out var parsed)
                && parsed;
        }

        /// <summary>
        /// Registers every configured source with the catalog and as an identity provider of the
        /// application.
        /// </summary>
        /// <remarks>
        /// A source that cannot be built - the framework refuses an authority or callback that
        /// is not https, and a callback that does not name its application and provider - is
        /// logged and skipped, so one bad entry costs its own accounts their sign-in and nothing
        /// else. The accounts of a skipped source cannot sign in at all, which is the answer for
        /// an account whose source is not installed.
        /// </remarks>
        /// <param name="pluginSettings">The settings of the core plugin. May be null.</param>
        /// <param name="applicationContext">The application the providers serve.</param>
        /// <param name="componentHub">The component hub.</param>
        /// <returns>The registered sources.</returns>
        public static IReadOnlyList<OpenIdConnectAuthenticationSource> RegisterConfigured
        (
            IConfiguration pluginSettings,
            IApplicationContext applicationContext,
            IComponentHub componentHub
        )
        {
            var registered = new List<OpenIdConnectAuthenticationSource>();

            foreach (var section in pluginSettings?.GetSection(Section).GetChildren() ?? [])
            {
                var settings = new OpenIdConnectSourceSettings();
                section.Bind(settings);

                try
                {
                    if (IdentitySource.IsLocal(settings.Key))
                    {
                        throw new ArgumentException($"An OpenID Connect source needs a key other than '{IdentitySource.Local}'.");
                    }

                    var source = new OpenIdConnectAuthenticationSource(settings);

                    AuthenticationSourceCatalog.Register(source);
                    componentHub?.IdentityProviderManager?.Register(source, applicationContext);

                    registered.Add(source);
                }
                catch (Exception ex)
                {
                    componentHub?.LogManager?.DefaultLog?.Warning
                    (
                        $"The OpenID Connect source '{settings.Key}' ({section.Path}) was not registered: {ex.Message}"
                    );
                }
            }

            return registered;
        }
    }

    /// <summary>
    /// The configuration of one OpenID Connect source, an entry of
    /// <c>Plugins:kleenestar.core:Authentication:OpenIdConnect</c>.
    /// </summary>
    public sealed class OpenIdConnectSourceSettings
    {
        /// <summary>
        /// Gets or sets the key the source's accounts carry, and the provider id the callback
        /// names (<c>provider=</c>). Must not be <c>local</c>.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the name the source is shown under.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the https issuer of the identity provider.
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// Gets or sets the client id this installation is registered under.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Gets or sets the client secret, for a confidential client.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Gets or sets the registered https callback:
        /// <c>https://host/api/auth/callback?application={application id}&amp;provider={Key}</c> -
        /// the framework refuses a callback that does not name both.
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// Gets or sets whether a sign-in that stands for no stored account creates one.
        /// Without it, an administrator creates the account for this source first and its first
        /// sign-in claims it by verified e-mail address.
        /// </summary>
        public bool Provision { get; set; }
    }
}
