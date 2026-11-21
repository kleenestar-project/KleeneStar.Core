using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Profile.Tokens
{
    /// <summary>
    /// Serves the personal access tokens of the calling identity to the forms on the token page
    /// and takes their creations and updates.
    /// </summary>
    /// <remarks>
    /// Reads and writes are confined to the caller's own tokens. Creating one is the single
    /// moment its secret exists in readable form: it is returned in the result message and
    /// stored only as a hash, so it can never be shown again.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<AccessToken>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the tokens of the calling identity that match the query.
        /// </summary>
        /// <param name="query">The query criteria used to select the tokens.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The matching tokens of the caller (possibly empty).</returns>
        protected override IEnumerable<AccessToken> Retrieve
        (
            IQuery<AccessToken> query,
            IQueryContext context,
            IRequest request
        )
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (ownerId == Guid.Empty)
            {
                return [];
            }

            return query
                .Apply(CoreHub.AccessTokenManager.GetAccessTokens(ownerId).AsQueryable())
                .Select(Sanitize);
        }

        /// <summary>
        /// Blanks the stored hash of a token before it leaves the server.
        /// </summary>
        /// <remarks>
        /// The REST serializer reflects over every public property and does not honour
        /// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>, so the hash would
        /// otherwise reach the client on every read of the token list. Blanking it is safe for
        /// the save path as well, because an update only ever writes the label, the scopes and
        /// the dates — never the credential.
        /// </remarks>
        /// <param name="token">The token about to be serialized. May be null.</param>
        /// <returns>The same instance with its hash cleared.</returns>
        private static AccessToken Sanitize(AccessToken token)
        {
            if (token is not null)
            {
                token.TokenHash = null;
            }

            return token;
        }

        /// <summary>
        /// Creates a token for the calling identity and reports its secret, which is the only
        /// time it can be read.
        /// </summary>
        /// <param name="fieldMap">The payload holding the label, the scopes and the lifetime.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <param name="newItem">When the method returns, contains the created token.</param>
        /// <returns>A result carrying the secret to hand to the owner.</returns>
        protected override IRestApiCrudResultCreate Create
        (
            RestApiCrudFormData fieldMap,
            IRequest request,
            out AccessToken newItem
        )
        {
            var draft = new AccessToken();
            fieldMap.BindTo(draft);

            newItem = CoreHub.AccessTokenManager.Create
            (
                request,
                draft.Name,
                draft.Scopes,
                draft.Expires.HasValue ? draft.Expires.Value - DateTime.UtcNow : null,
                out var secret
            );

            if (newItem is null)
            {
                return null;
            }

            return new RestApiCrudResultCreate()
            {
                Message = secret
            };
        }

        /// <summary>
        /// Renames a token or changes the scopes it grants. The secret is never touched — a
        /// token that has to change its secret is revoked and created anew.
        /// </summary>
        /// <param name="existingItem">The currently persisted token.</param>
        /// <param name="payload">The dynamic payload containing the edited fields.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>A result object containing information about the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update
        (
            AccessToken existingItem,
            RestApiCrudFormData payload,
            IRequest request
        )
        {
            var editable = new RestApiCrudFormData();

            foreach (var entry in payload.Where(x => EditableFields.Contains(x.Key)))
            {
                editable[entry.Key] = entry.Value;
            }

            var res = base.Update(existingItem, editable, request);

            CoreHub.AccessTokenManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes a token of the calling identity.
        /// </summary>
        /// <param name="existingItem">The token to delete.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>A result object containing information about the delete operation.</returns>
        protected override IRestApiCrudResultDelete Delete(AccessToken existingItem, IRequest request)
        {
            CoreHub.AccessTokenManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// The token fields the profile forms own, keyed the way the payload names them. The
        /// prefix, the hash and the creation date describe what the token is and are not up
        /// for editing.
        /// </summary>
        private static readonly HashSet<string> EditableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(AccessToken.Name),
            nameof(AccessToken.Scopes),
            nameof(AccessToken.Expires)
        };
    }
}
