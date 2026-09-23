using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using System;
using System.Linq;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// The credential a request is authenticated by: the identity WebExpress verified, and the
    /// token it verified it from.
    /// </summary>
    /// <remarks>
    /// WebExpress answers <em>who</em>; KleeneStar also needs <em>by what</em>, because two
    /// questions are asked of the token itself. A sign-in (<see cref="Personal"/> false) is a
    /// grant, and the grant is what the session list shows and what revoking a session ends -
    /// the framework revokes grants in its token store but does not check an access token
    /// against the store, so <see cref="WebManager.SessionManager"/> does. A personal access
    /// token is matched to the row the profile issued it as, for its scopes and its
    /// revocation.
    /// <para>
    /// <b>One sign-in serves every KleeneStar application.</b> A WebExpress token is bound to the
    /// application it was issued for, and the portal is an application of its own - so the
    /// token the core's sign-in issues was refused on every portal page and the portal was
    /// anonymous for everybody. A request of another <c>kleenestar.*</c> application that the
    /// framework answers with nobody is therefore asked again against the core application.
    /// The trust is deliberate and bounded: the applications are one product of one
    /// installation, and an application of another plugin is not given the core's credential.
    /// </para>
    /// </remarks>
    /// <param name="Identity">The identity the token names.</param>
    /// <param name="Token">The verified token, or <see langword="null"/> when none could be read.</param>
    /// <param name="Personal">Whether the token is a personal access token rather than a sign-in.</param>
    /// <param name="Application">The application the token was issued for.</param>
    public sealed record SessionCredential(IIdentity Identity, JsonWebToken Token, bool Personal, IApplicationContext Application)
    {
        /// <summary>
        /// The name of the access cookie over https (and the framework's protected prefix).
        /// </summary>
        private const string SecureAccessCookie = "__Host-wx-access";

        /// <summary>
        /// The name of the access cookie over plain http.
        /// </summary>
        private const string DevelopmentAccessCookie = "wx-access";

        /// <summary>
        /// Gets the grant a sign-in belongs to, or <see langword="null"/> for a personal token.
        /// </summary>
        public string GrantId => !Personal && Token is not null && Token.TryGetPayloadValue<string>("grant", out var grant) ? grant : null;

        /// <summary>
        /// Gets the point in time the grant ends however often it is refreshed.
        /// </summary>
        public DateTime? GrantExpires => !Personal && Token is not null && Token.TryGetPayloadValue<long>("grant_exp", out var expires)
            ? DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime
            : null;

        /// <summary>
        /// Gets the point in time the token itself stops being accepted.
        /// </summary>
        public DateTime? Expires => Token is null ? null : Token.ValidTo;

        /// <summary>
        /// Gets the id (<c>jti</c>) of the token.
        /// </summary>
        public string TokenId => Token?.Id;

        /// <summary>
        /// Reads the credential of a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="componentHub">The component hub.</param>
        /// <returns>The credential, or <see langword="null"/> when the request carries none that
        /// verifies.</returns>
        public static SessionCredential Read(IRequest request, IComponentHub componentHub)
        {
            var identityManager = componentHub?.IdentityManager;

            if (request is null || identityManager is null)
            {
                return null;
            }

            var application = request.ApplicationContext;
            var bearer = Bearer(request);
            var raw = bearer ?? AccessCookie(request);
            var identity = identityManager.GetCurrentIdentity(request);

            // another KleeneStar application shares the core's sign-in (see remarks)
            var core = CoreHub.ApplicationContext;

            if (identity is null && raw is not null && core is not null && IsSibling(application, core)
                && identityManager is IdentityManager concrete)
            {
                identity = bearer is not null
                    ? concrete.ValidatePersonalAccessToken(bearer, core)
                    : concrete.ValidateAccessToken(raw, core);
                application = core;
            }

            if (identity is null)
            {
                return null;
            }

            return new SessionCredential(identity, Parse(raw), bearer is not null, application);
        }

        /// <summary>
        /// Determines whether an application is another application of KleeneStar than the core.
        /// </summary>
        /// <param name="application">The application the request is served by.</param>
        /// <param name="core">The core application.</param>
        /// <returns><see langword="true"/> for a sibling application.</returns>
        private static bool IsSibling(IApplicationContext application, IApplicationContext core)
        {
            return application is not null
                && !string.Equals(application.ApplicationId, core.ApplicationId, StringComparison.Ordinal)
                && (application.PluginContext?.PluginId?.ToString()?.StartsWith("kleenestar.", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        /// <summary>
        /// Reads the bearer token of the request, if it carries one.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The token, or <see langword="null"/>.</returns>
        private static string Bearer(IRequest request)
        {
            var authorization = request.Header?.Authorization;

            return authorization is not null && string.Equals(authorization.Type, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? authorization.Token
                : null;
        }

        /// <summary>
        /// Reads the access cookie of the request - the framework's name for the transport the
        /// installation is configured for, and only when it is carried exactly once, like the
        /// framework reads it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The token, or <see langword="null"/>.</returns>
        private static string AccessCookie(IRequest request)
        {
            var secure = CoreHub.HttpServerContext?.Configuration?.GetValue("WebExpress:Authentication:RequireHttps", true) ?? true;
            var name = secure ? SecureAccessCookie : DevelopmentAccessCookie;
            var cookies = request.Header?.Cookies?.Where(x => x.Name == name).ToArray() ?? [];

            return cookies.Length == 1 && !string.IsNullOrEmpty(cookies[0].Value) ? cookies[0].Value : null;
        }

        /// <summary>
        /// Reads the claims of a token the framework has already verified.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>The token, or <see langword="null"/> when it is not one.</returns>
        private static JsonWebToken Parse(string token)
        {
            try
            {
                return string.IsNullOrEmpty(token) ? null : new JsonWebToken(token);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
