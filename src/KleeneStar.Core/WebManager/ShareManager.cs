using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages share relationships between objects and identities. A share grants
    /// the linked identity read/comment access to the object (e.g. a portal issue)
    /// without making it the requester.
    /// </summary>
    public sealed class ShareManager : IShareManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a share has been granted via <see cref="Add"/>.
        /// </summary>
        public event EventHandler<ObjectShare> ShareAdded;

        /// <summary>
        /// Raised after a share has been revoked via <see cref="Remove"/>.
        /// </summary>
        public event EventHandler<ObjectShare> ShareRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ShareManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns every share attached to the object addressed by the supplied
        /// URL-bound object-key parameter, in chronological order (oldest first).
        /// </summary>
        /// <param name="objectKey">The object-key parameter.</param>
        /// <returns>The shares attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectShare> GetShares(ObjectKeyParameter objectKey)
        {
            ArgumentNullException.ThrowIfNull(objectKey);

            // through the object manager, so a key the caller may not read - by its grants
            // or its security level - has nothing to show
            var obj = CoreHub.ObjectManager.GetObjectByKey(objectKey.Value);
            if (obj is null)
            {
                return [];
            }

            return GetShares(obj.Id);
        }

        /// <summary>
        /// Returns every share attached to the object with the supplied id, in
        /// chronological order (oldest first).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The shares attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectShare> GetShares(Guid objectId)
        {
            var query = new Query<ObjectShare>()
                .WhereEquals(x => x.ObjectId, objectId);

            return ModelHub.GetObjectShares(query).OrderBy(s => s.Created).ToList();
        }

        /// <summary>
        /// Returns the shares that satisfy the supplied query. The manager opens its
        /// own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching shares.</returns>
        public IEnumerable<ObjectShare> GetShares(IQuery<ObjectShare> query)
        {
            return ModelHub.GetObjectShares(query);
        }

        /// <summary>
        /// Returns the shares that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching shares.</returns>
        public IEnumerable<ObjectShare> GetShares(IQuery<ObjectShare> query, IQueryContext context)
        {
            return ModelHub.GetObjectShares(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Grants the supplied identity access to the supplied object. When the
        /// identity already holds a share on the object, the existing row is returned
        /// and <see cref="ShareAdded"/> is NOT re-raised. Returns <see langword="null"/>
        /// when either side does not exist.
        /// </summary>
        /// <param name="objectId">The id of the shared object.</param>
        /// <param name="identityId">The id of the identity the object is shared with.</param>
        /// <returns>The persisted share relationship, or <see langword="null"/>.</returns>
        public ObjectShare Add(Guid objectId, Guid identityId)
        {
            using var db = ModelHub.CreateDbContext();

            var objectExists = db.Objects.AsNoTracking().Any(o => o.Id == objectId);
            var identityExists = db.Identities.AsNoTracking().Any(i => i.Id == identityId);
            if (!objectExists || !identityExists)
            {
                return null;
            }

            var existing = db.ObjectShares
                .AsNoTracking()
                .FirstOrDefault(s => s.ObjectId == objectId && s.IdentityId == identityId);

            if (existing is not null)
            {
                return existing;
            }

            var share = new ObjectShare
            {
                ObjectId = objectId,
                IdentityId = identityId,
                Created = DateTime.UtcNow
            };

            ModelHub.Add(share);
            ShareAdded?.Invoke(this, share);
            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.share.created", CoreHub.ObjectManager.GetObject(objectId));

            return share;
        }

        /// <summary>
        /// Revokes the share between the supplied object and identity. Raises
        /// <see cref="ShareRemoved"/> when a row existed.
        /// </summary>
        /// <param name="objectId">The id of the shared object.</param>
        /// <param name="identityId">The id of the identity whose share is revoked.</param>
        /// <returns><see langword="true"/> when a row existed and was removed.</returns>
        public bool Remove(Guid objectId, Guid identityId)
        {
            using var db = ModelHub.CreateDbContext();
            var existing = db.ObjectShares
                .FirstOrDefault(s => s.ObjectId == objectId && s.IdentityId == identityId);

            if (existing is null)
            {
                return false;
            }

            ModelHub.Remove(existing);
            ShareRemoved?.Invoke(this, existing);
            TryAddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.share.deleted", CoreHub.ObjectManager.GetObject(objectId));

            return true;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Emits a UI notification via <see cref="CoreHub.AddNotification"/>, swallowing
        /// any exception so that tests with a partially wired host don't crash.
        /// </summary>
        /// <param name="titleKey">The i18n key of the notification title.</param>
        /// <param name="messageKey">The i18n key of the notification message.</param>
        private static void TryAddNotification(string titleKey, string messageKey, object subject)
        {
            try
            {
                CoreHub.AddNotification(titleKey, messageKey, subject);
            }
            catch
            {
                // notification is best-effort; ignore failures from incomplete host state
            }
        }
    }
}
