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
    /// grant, and the grant is what the session list shows and what revoking a session ends; the
    /// framework revokes grants in its token store and checks the store on a refresh, and
    /// <see cref="WebManager.ISessionManager.Accepts"/> checks it on every request. A personal
    /// access token is matched to the row the profile issued it as, for its scopes and its
    /// revocation.
    /// <para>
    /// Core and portal form the application group <c>kleenestar</c>
    /// (<c>WebExpress:Authentication:ApplicationGroups</c>), so the framework accepts the
    /// sign-in of either application in both. Every KleeneStar sign-in is a grant of the core
    /// application; <see cref="Application"/> is therefore the core application, whose token
    /// store holds the revocations.
    /// </para>
    /// </remarks>
    /// <param name="Identity">The identity the token names.</param>
    /// <param name="Token">The verified token, or <see langword="null"/> when none could be read
    /// or the credential was built from the framework's validation context (<see cref="From"/>).</param>
    /// <param name="Personal">Whether the token is a personal access token rather than a sign-in.</param>
    /// <param name="Application">The application whose token store holds the grant.</param>
    public sealed record SessionCredential(IIdentity Identity, JsonWebToken Token, bool Personal, IApplicationContext Application)
    {
        /// <summary>
        /// The token use of a personal access token.
        /// </summary>
        private const string PersonalTokenUse = "pat";

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
        public string GrantId { get; init; } = Personal ? null : Claim(Token, "grant");

        /// <summary>
        /// Gets the point in time the grant ends however often it is refreshed, which is how
        /// long the session is listed and how long revoking it must last.
        /// </summary>
        public DateTime? GrantExpires { get; init; } = !Personal && Token is not null && Token.TryGetPayloadValue<long>("grant_exp", out var expires)
            ? DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime
            : null;

        /// <summary>
        /// Gets the point in time the token itself stops being accepted.
        /// </summary>
        public DateTime? Expires { get; init; } = Token?.ValidTo;

        /// <summary>
        /// Gets the id (<c>jti</c>) of the token.
        /// </summary>
        public string TokenId { get; init; } = string.IsNullOrEmpty(Token?.Id) ? null : Token.Id;

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

            var identity = identityManager.GetCurrentIdentity(request);

            if (identity is null)
            {
                return null;
            }

            var bearer = Bearer(request);
            var raw = bearer ?? AccessCookie(request);

            return new SessionCredential(identity, Parse(raw), bearer is not null, CoreHub.ApplicationContext ?? request.ApplicationContext);
        }

        /// <summary>
        /// Builds the credential the framework asks an <see cref="IIdentityValidator"/> about.
        /// </summary>
        /// <remarks>
        /// Everything the credential knows of the token comes from the context, which the
        /// framework read from the token it verified. The request is not consulted: on a refresh
        /// it still carries the previous access cookie, which nobody verified, and a forged one
        /// would otherwise set the end of the grant - how long the session is listed and how long
        /// revoking it lasts. <see cref="Token"/> is therefore <see langword="null"/>.
        /// </remarks>
        /// <param name="context">The validation context.</param>
        /// <returns>The credential, or <see langword="null"/> when the context names no identity.</returns>
        public static SessionCredential From(IdentityValidationContext context)
        {
            if (context?.Identity is null)
            {
                return null;
            }

            var personal = string.Equals(context.TokenUse, PersonalTokenUse, StringComparison.Ordinal);

            return new SessionCredential(context.Identity, null, personal, CoreHub.ApplicationContext ?? context.ApplicationContext)
            {
                GrantId = personal ? null : context.Grant,
                GrantExpires = personal ? null : context.GrantExpiresAt?.UtcDateTime,
                Expires = context.ExpiresAt == default ? null : context.ExpiresAt.UtcDateTime,
                TokenId = context.TokenId
            };
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
        /// Reads a text claim of a token.
        /// </summary>
        /// <param name="token">The token, or <see langword="null"/>.</param>
        /// <param name="name">The claim.</param>
        /// <returns>The value, or <see langword="null"/> when the token carries none.</returns>
        private static string Claim(JsonWebToken token, string name)
        {
            return token is not null && token.TryGetPayloadValue<string>(name, out var value) ? value : null;
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
