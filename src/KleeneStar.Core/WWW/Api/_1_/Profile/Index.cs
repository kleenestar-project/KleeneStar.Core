using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Profile
{
    /// <summary>
    /// Serves the profile settings of the calling identity to the forms on the profile pages
    /// and takes their updates.
    /// </summary>
    /// <remarks>
    /// The endpoint is deliberately narrower than <see cref="Identities.Index"/>: every read
    /// and every write is confined to the identity the request is served for, so a caller who
    /// passes somebody else's id gets nothing rather than a foreign account to edit. Creating
    /// and deleting are not part of the contract — an account is created and removed through
    /// the identity administration, not through its own profile.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Identity>
    {
        /// <summary>
        /// The identity fields the profile forms own, keyed the way the payload names them
        /// (lower case). Anything outside this set is ignored on update.
        /// </summary>
        private static readonly HashSet<string> EditableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            // profile page
            nameof(Model.Entities.Identity.Name),
            nameof(Model.Entities.Identity.Avatar),
            nameof(Model.Entities.Identity.Bio),
            nameof(Model.Entities.Identity.PhoneCountry),
            nameof(Model.Entities.Identity.Phone),
            nameof(Model.Entities.Identity.Website),
            nameof(Model.Entities.Identity.Location),
            nameof(Model.Entities.Identity.Position),

            // account page
            nameof(Model.Entities.Identity.Language),
            nameof(Model.Entities.Identity.TimeZone),
            nameof(Model.Entities.Identity.DateFormat),
            nameof(Model.Entities.Identity.WeekStart),

            // tenant & role page — the role itself is set by the workspace admins
            nameof(Model.Entities.Identity.Department),
            nameof(Model.Entities.Identity.CostCenter),
            nameof(Model.Entities.Identity.DeputyId)
        };

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
        /// Retrieves the calling identity, ignoring any other identity the query may name.
        /// </summary>
        /// <param name="query">
        /// The query criteria. Not applied — the caller does not get to choose whose profile is
        /// returned.
        /// </param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>
        /// A collection holding the calling identity, or an empty one when no identity can be
        /// resolved for the request.
        /// </returns>
        protected override IEnumerable<Model.Entities.Identity> Retrieve
        (
            IQuery<Model.Entities.Identity> query,
            IQueryContext context,
            IRequest request
        )
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return [];
            }

            var own = new Query<Model.Entities.Identity>()
                .WhereEquals(x => x.Id, identityId)
                .WithPaging(0, 1);

            return CoreHub.IdentityManager
                .GetIdentities(own, context)
                .Select(Sanitize);
        }

        /// <summary>
        /// Blanks the credential fields of an identity before it leaves the server.
        /// </summary>
        /// <remarks>
        /// The REST serializer reflects over every public property and does not honour
        /// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>, so a property that
        /// is invisible to a plain serialization still reaches the client here. The password
        /// hash is nothing the profile form has any use for, so it is removed from the copy
        /// that is serialized.
        /// </remarks>
        /// <param name="identity">The identity about to be serialized. May be null.</param>
        /// <returns>The same instance with its credential fields cleared.</returns>
        private static Model.Entities.Identity Sanitize(Model.Entities.Identity identity)
        {
            if (identity is not null)
            {
                identity.PasswordHash = null;
            }

            return identity;
        }

        /// <summary>
        /// Persists the edited profile settings of the calling identity.
        /// </summary>
        /// <remarks>
        /// Only the fields the profile forms own are taken from the payload. Everything else an
        /// identity carries — its state, its password, the tenant it belongs to, the role the
        /// workspace admins assigned — is administered elsewhere and must not become writable
        /// just because it sits on the same record as the user's own settings.
        /// </remarks>
        /// <param name="existingItem">The currently persisted identity.</param>
        /// <param name="payload">The dynamic payload containing the edited settings.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>A result object containing information about the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update
        (
            Model.Entities.Identity existingItem,
            RestApiCrudFormData payload,
            IRequest request
        )
        {
            var editable = new RestApiCrudFormData();

            foreach (var entry in payload.Where(x => EditableFields.Contains(x.Key)))
            {
                editable[entry.Key] = entry.Value;
            }

            // the picture is taken out of the payload and stored separately: the avatar control
            // submits it inline as a data url, which the binder would hand to
            // RestValueConverterImageIcon and end up as the URI "http:///" - while the request
            // still answers 200. See RestApiCrudFormDataAvatarExtensions.
            var avatarSent = editable.Detach(nameof(Model.Entities.Identity.Avatar), out var avatar);

            // the item handed in came through Sanitize, which blanked the password hash on its
            // way to the client. Saving that copy would write the blank back, so the edits are
            // applied to a freshly read record instead.
            var persisted = CoreHub.IdentityManager.GetIdentity(existingItem.Id) ?? existingItem;

            var res = base.Update(persisted, editable, request);

            if (avatarSent)
            {
                persisted.Avatar = RestApiCrudFormDataAvatarExtensions.Resolve
                (
                    persisted.Id,
                    avatar,
                    persisted.Avatar,
                    () => CoreHub.GenerateIcon(persisted.Id)
                );
            }

            // an identity must not stand in for itself — the deputy would be the very account
            // that is absent
            if (persisted.DeputyId == persisted.Id)
            {
                persisted.DeputyId = null;
            }

            CoreHub.IdentityManager.Update(persisted);

            return res;
        }

        /// <summary>
        /// Deletes nothing. An account is removed through the identity administration, not
        /// through its own profile settings.
        /// </summary>
        /// <param name="existingItem">The identity the request addressed.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>A result object reporting that nothing was deleted.</returns>
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Identity existingItem, IRequest request)
        {
            return new RestApiCrudResultDelete();
        }
    }
}
