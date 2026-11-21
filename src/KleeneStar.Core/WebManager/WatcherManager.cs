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
    /// Manages watch relationships between objects and identities.
    /// </summary>
    public sealed class WatcherManager : IWatcherManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a watch has been added via <see cref="Add"/>.
        /// </summary>
        public event EventHandler<ObjectWatcher> WatcherAdded;

        /// <summary>
        /// Raised after a watch has been removed via <see cref="Remove"/>.
        /// </summary>
        public event EventHandler<ObjectWatcher> WatcherRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WatcherManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns every watcher attached to the object addressed by the supplied
        /// URL-bound object-key parameter, in chronological order (oldest first).
        /// </summary>
        /// <param name="objectKey">The object-key parameter.</param>
        /// <returns>The watchers attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectWatcher> GetWatchers(ObjectKeyParameter objectKey)
        {
            ArgumentNullException.ThrowIfNull(objectKey);

            // through the object manager, so a key the caller may not read - by its grants
            // or its security level - has nothing to show
            var obj = CoreHub.ObjectManager.GetObjectByKey(objectKey.Value);
            if (obj is null)
            {
                return [];
            }

            return GetWatchers(obj.Id);
        }

        /// <summary>
        /// Returns every watcher attached to the object with the supplied id, in
        /// chronological order (oldest first).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The watchers attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectWatcher> GetWatchers(Guid objectId)
        {
            var query = new Query<ObjectWatcher>()
                .WhereEquals(x => x.ObjectId, objectId);

            return ModelHub.GetObjectWatchers(query).OrderBy(w => w.Created).ToList();
        }

        /// <summary>
        /// Returns the watchers that satisfy the supplied query. The manager opens its
        /// own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching watchers.</returns>
        public IEnumerable<ObjectWatcher> GetWatchers(IQuery<ObjectWatcher> query)
        {
            return ModelHub.GetObjectWatchers(query);
        }

        /// <summary>
        /// Returns the watchers that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching watchers.</returns>
        public IEnumerable<ObjectWatcher> GetWatchers(IQuery<ObjectWatcher> query, IQueryContext context)
        {
            return ModelHub.GetObjectWatchers(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a watch relationship between the supplied object and identity. When the
        /// identity is already watching the object, the existing row is returned and
        /// <see cref="WatcherAdded"/> is NOT re-raised. Returns <see langword="null"/>
        /// when either side does not exist.
        /// </summary>
        /// <param name="objectId">The id of the object being watched.</param>
        /// <param name="identityId">The id of the watching identity.</param>
        /// <returns>The persisted watch relationship, or <see langword="null"/>.</returns>
        public ObjectWatcher Add(Guid objectId, Guid identityId)
        {
            using var db = ModelHub.CreateDbContext();

            var objectExists = db.Objects.AsNoTracking().Any(o => o.Id == objectId);
            var identityExists = db.Identities.AsNoTracking().Any(i => i.Id == identityId);
            if (!objectExists || !identityExists)
            {
                return null;
            }

            var existing = db.ObjectWatchers
                .AsNoTracking()
                .FirstOrDefault(w => w.ObjectId == objectId && w.IdentityId == identityId);

            if (existing is not null)
            {
                return existing;
            }

            var watcher = new ObjectWatcher
            {
                ObjectId = objectId,
                IdentityId = identityId,
                Created = DateTime.UtcNow
            };

            ModelHub.Add(watcher);
            WatcherAdded?.Invoke(this, watcher);
            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.watcher.created", CoreHub.ObjectManager.GetObject(objectId));

            return watcher;
        }

        /// <summary>
        /// Removes the watch relationship between the supplied object and identity.
        /// Raises <see cref="WatcherRemoved"/> when a row existed.
        /// </summary>
        /// <param name="objectId">The id of the watched object.</param>
        /// <param name="identityId">The id of the watching identity.</param>
        /// <returns><see langword="true"/> when a row existed and was removed.</returns>
        public bool Remove(Guid objectId, Guid identityId)
        {
            using var db = ModelHub.CreateDbContext();
            var existing = db.ObjectWatchers
                .FirstOrDefault(w => w.ObjectId == objectId && w.IdentityId == identityId);

            if (existing is null)
            {
                return false;
            }

            ModelHub.Remove(existing);
            WatcherRemoved?.Invoke(this, existing);
            TryAddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.watcher.deleted", CoreHub.ObjectManager.GetObject(objectId));

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
