using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the personal access tokens an identity created for API access and integrations.
    /// </summary>
    /// <remarks>
    /// A token is a WebExpress personal access token: <see cref="Create"/> has the framework
    /// sign one for the caller, and a client presents it as <c>Authorization: Bearer</c>. The
    /// framework verifies the signature, the lifetime and its own revocation list; this manager
    /// keeps what the owner sees - name, scopes, dates - and answers, per request, whether the
    /// token is still one the profile lists and whether its scopes allow the request
    /// (<see cref="Accepts"/>). A token the framework issued by other means is refused: every
    /// credential that can act for an account is on that account's token list.
    /// <para>
    /// The secret exists exactly once: it is handed to the caller, and only its hash, its id
    /// (<c>jti</c>) and a recognizable tail are stored. Revoking or deleting a token revokes it
    /// in the framework's token store, so it stops working on every node sharing the store.
    /// </para>
    /// <para>
    /// The scopes are coarse on purpose: a token without any <c>write:</c> scope is read-only -
    /// it acts for its owner on reads and as nobody on writes. Which resource a <c>write:</c>
    /// scope names is not checked yet.
    /// </para>
    /// </remarks>
    public sealed class AccessTokenManager : IAccessTokenManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// The number of trailing characters of the secret that are kept in clear, so the owner
        /// can tell one token from another. The head of every token is the same encoded
        /// header; the tail is signature.
        /// </summary>
        private const int VisibleLength = 8;

        /// <summary>
        /// How often a token's last use is written at most.
        /// </summary>
        private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// When each token's last use was last written by this process.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, DateTime> _touched = new();

        /// <summary>
        /// An event that fires when a token is created.
        /// </summary>
        public event EventHandler<AccessToken> AccessTokenAdded;

        /// <summary>
        /// An event that fires when a token is updated or revoked.
        /// </summary>
        public event EventHandler<AccessToken> AccessTokenUpdated;

        /// <summary>
        /// An event that fires when a token is deleted.
        /// </summary>
        public event EventHandler<AccessToken> AccessTokenRemoved;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private AccessTokenManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the tokens of the identity the request is served for, newest first.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>An enumerable collection of tokens (possibly empty).</returns>
        public IEnumerable<AccessToken> GetAccessTokens(IRequest request)
        {
            return GetAccessTokens(CoreHub.SessionManager.GetCurrentIdentityId(request));
        }

        /// <summary>
        /// Returns the tokens owned by the given identity, newest first.
        /// </summary>
        /// <param name="ownerId">The identity that owns the tokens.</param>
        /// <returns>An enumerable collection of tokens (possibly empty).</returns>
        public IEnumerable<AccessToken> GetAccessTokens(Guid ownerId)
        {
            return ModelHub.GetAccessTokens(ownerId);
        }

        /// <summary>
        /// Returns a token by its id.
        /// </summary>
        /// <param name="tokenId">The id of the token.</param>
        /// <returns>The token, or <see langword="null"/> when no such token exists.</returns>
        public AccessToken GetAccessToken(Guid tokenId)
        {
            return ModelHub.GetAccessToken(tokenId);
        }

        /// <summary>
        /// Creates a token for the identity the request is served for and returns the secret
        /// exactly once — it is stored hashed and can never be read again.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="name">The label the owner gave the token.</param>
        /// <param name="scopes">The scopes the token grants, separated by spaces.</param>
        /// <param name="lifetime">
        /// How long the token is valid, or <see langword="null"/> when it never expires.
        /// </param>
        /// <param name="secret">
        /// When the method returns, contains the token secret to hand to the owner.
        /// </param>
        /// <returns>The created token.</returns>
        public AccessToken Create(IRequest request, string name, string scopes, TimeSpan? lifetime, out string secret)
        {
            secret = null;

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var owner = ownerId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(ownerId);
            var issuer = Hub?.IdentityManager as WebExpress.WebCore.WebIdentity.IdentityManager;
            var application = CoreHub.ApplicationContext;

            if (owner is null || issuer is null || application is null)
            {
                return null;
            }

            // the framework caps a personal token's lifetime; "never expires" is the longest
            // it allows, and a longer wish is shortened to it rather than refused
            var maximum = MaximumLifetime();
            var effective = lifetime is null || lifetime.Value > maximum ? maximum : lifetime.Value;

            if (effective < TimeSpan.FromMinutes(1))
            {
                return null;
            }

            // a KleeneStar identity carries no framework permissions, so the token carries none
            // either; what it may do is its owner's groups, narrowed by its scopes (Accepts)
            secret = issuer.CreatePersonalAccessToken(owner, application, effective, []);

            var jwt = new JsonWebToken(secret);

            var token = new AccessToken
            {
                OwnerId = ownerId,
                Name = string.IsNullOrWhiteSpace(name) ? "Token" : name.Trim(),
                Prefix = "…" + secret[^VisibleLength..],
                TokenId = jwt.Id,
                TokenHash = Hash(secret),
                Scopes = scopes,
                Created = DateTime.UtcNow,
                Expires = jwt.ValidTo
            };

            ModelHub.Add(token);

            AccessTokenAdded?.Invoke(this, token);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.token.created", token);

            return token;
        }

        /// <summary>
        /// Updates a token.
        /// </summary>
        /// <param name="token">The token to update.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IAccessTokenManager Update(AccessToken token)
        {
            ArgumentNullException.ThrowIfNull(token);

            ModelHub.Update(token);

            AccessTokenUpdated?.Invoke(this, token);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.token.updated", token);

            return this;
        }

        /// <summary>
        /// Revokes the token with the given id, so it stops authenticating requests without
        /// disappearing from the owner's audit trail.
        /// </summary>
        /// <param name="tokenId">The id of the token to revoke.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IAccessTokenManager Revoke(Guid tokenId)
        {
            var token = GetAccessToken(tokenId);

            if (token is null || token.Revoked)
            {
                return this;
            }

            RevokeInStore(token);

            token.Revoked = true;
            ModelHub.Update(token);

            AccessTokenUpdated?.Invoke(this, token);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.token.revoked", token);

            return this;
        }

        /// <summary>
        /// Deletes the token with the given id.
        /// </summary>
        /// <param name="tokenId">The id of the token to delete.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IAccessTokenManager Remove(Guid tokenId)
        {
            var token = GetAccessToken(tokenId);

            if (token is null)
            {
                return this;
            }

            // deleting the row must not leave a token that still works and that nobody can see
            if (!token.Revoked)
            {
                RevokeInStore(token);
            }

            ModelHub.RemoveAccessToken(tokenId);

            AccessTokenRemoved?.Invoke(this, token);

            CoreHub.AddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.token.deleted", token);

            return this;
        }

        /// <summary>
        /// Decides whether a personal access token may act for its owner on this request, and
        /// records that it did.
        /// </summary>
        /// <param name="credential">The verified personal token.</param>
        /// <param name="request">The request it came with.</param>
        /// <returns><see langword="true"/> when the token may act.</returns>
        public bool Accepts(WebIdentity.SessionCredential credential, IRequest request)
        {
            var token = ModelHub.GetAccessTokenByTokenId(credential?.TokenId);

            if (token is null || token.OwnerId != credential.Identity?.Id || token.State != AccessTokenState.Active)
            {
                return false;
            }

            if (IsWrite(request) && !AllowsWrites(token))
            {
                return false;
            }

            var now = DateTime.UtcNow;

            if (!_touched.TryGetValue(token.Id, out var last) || now - last >= TouchInterval)
            {
                _touched[token.Id] = now;

                try
                {
                    ModelHub.TouchAccessToken(token.Id, now);
                }
                catch (Exception ex)
                {
                    Hub?.LogManager?.DefaultLog?.Exception(ex);
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a token grants any write scope.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="true"/> when it may write.</returns>
        internal static bool AllowsWrites(AccessToken token)
        {
            return (token?.Scopes ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => x.StartsWith("write:", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether a request changes something.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for every method but the reading ones.</returns>
        private static bool IsWrite(IRequest request)
        {
            return request is not null
                && request.Method is not (RequestMethod.GET or RequestMethod.HEAD);
        }

        /// <summary>
        /// Revokes a token in the framework's token store.
        /// </summary>
        /// <param name="token">The token.</param>
        private void RevokeInStore(AccessToken token)
        {
            if (string.IsNullOrEmpty(token?.TokenId) || CoreHub.ApplicationContext is null)
            {
                return;
            }

            var expires = token.Expires is { } at && at > DateTime.UtcNow ? at : DateTime.UtcNow.AddMinutes(1);

            Hub?.IdentityTokenStoreManager?.GetStore(CoreHub.ApplicationContext)
                ?.Revoke("pat:" + token.TokenId, new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc), TimeSpan.Zero));
        }

        /// <summary>
        /// Reads the longest lifetime the framework allows a personal token.
        /// </summary>
        /// <returns>The lifetime.</returns>
        private static TimeSpan MaximumLifetime()
        {
            return CoreHub.HttpServerContext?.Configuration?.GetValue<TimeSpan?>("WebExpress:Authentication:MaximumPersonalAccessTokenLifetime")
                ?? TimeSpan.FromDays(90);
        }

        /// <summary>
        /// Gets the component hub, also when the manager was built without one.
        /// </summary>
        private IComponentHub Hub => _componentHub ?? CoreHub.ComponentHub;

        /// <summary>
        /// Returns the hash under which the given secret is stored.
        /// </summary>
        /// <param name="secret">The token secret handed to the owner.</param>
        /// <returns>The hexadecimal SHA-256 hash of the secret.</returns>
        private static string Hash(string secret)
        {
            return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
