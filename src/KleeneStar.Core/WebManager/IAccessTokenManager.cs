using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the personal access tokens an identity created for
    /// API access and integrations.
    /// </summary>
    public interface IAccessTokenManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a token is created.
        /// </summary>
        event EventHandler<AccessToken> AccessTokenAdded;

        /// <summary>
        /// An event that fires when a token is updated or revoked.
        /// </summary>
        event EventHandler<AccessToken> AccessTokenUpdated;

        /// <summary>
        /// An event that fires when a token is deleted.
        /// </summary>
        event EventHandler<AccessToken> AccessTokenRemoved;

        /// <summary>
        /// Returns the tokens of the identity the request is served for, newest first.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>An enumerable collection of tokens (possibly empty).</returns>
        IEnumerable<AccessToken> GetAccessTokens(IRequest request);

        /// <summary>
        /// Returns the tokens owned by the given identity, newest first.
        /// </summary>
        /// <param name="ownerId">The identity that owns the tokens.</param>
        /// <returns>An enumerable collection of tokens (possibly empty).</returns>
        IEnumerable<AccessToken> GetAccessTokens(Guid ownerId);

        /// <summary>
        /// Returns a token by its id.
        /// </summary>
        /// <param name="tokenId">The id of the token.</param>
        /// <returns>The token, or <see langword="null"/> when no such token exists.</returns>
        AccessToken GetAccessToken(Guid tokenId);

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
        AccessToken Create(IRequest request, string name, string scopes, TimeSpan? lifetime, out string secret);

        /// <summary>
        /// Updates a token.
        /// </summary>
        /// <param name="token">The token to update.</param>
        /// <returns>The current instance for method chaining.</returns>
        IAccessTokenManager Update(AccessToken token);

        /// <summary>
        /// Revokes the token with the given id, so it stops authenticating requests without
        /// disappearing from the owner's audit trail.
        /// </summary>
        /// <param name="tokenId">The id of the token to revoke.</param>
        /// <returns>The current instance for method chaining.</returns>
        IAccessTokenManager Revoke(Guid tokenId);

        /// <summary>
        /// Deletes the token with the given id.
        /// </summary>
        /// <param name="tokenId">The id of the token to delete.</param>
        /// <returns>The current instance for method chaining.</returns>
        IAccessTokenManager Remove(Guid tokenId);

        /// <summary>
        /// Decides whether a personal access token may act for its owner on this request: it
        /// is one the profile lists, active, and its scopes allow the request.
        /// </summary>
        /// <param name="credential">The verified personal token.</param>
        /// <param name="request">The request it came with.</param>
        /// <returns><see langword="true"/> when the token may act.</returns>
        bool Accepts(WebIdentity.SessionCredential credential, IRequest request);
    }
}
