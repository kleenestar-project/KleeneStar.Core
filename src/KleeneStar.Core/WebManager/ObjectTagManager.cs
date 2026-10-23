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
    /// Manages tags (labels) attached to objects.
    /// </summary>
    public sealed class ObjectTagManager : IObjectTagManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a tag has been attached via <see cref="Add"/>.
        /// </summary>
        public event EventHandler<ObjectTag> TagAdded;

        /// <summary>
        /// Raised after a tag has been detached via <see cref="Remove"/>.
        /// </summary>
        public event EventHandler<ObjectTag> TagRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectTagManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns every tag attached to the object addressed by the supplied URL-bound
        /// object-key parameter, in chronological order (oldest first).
        /// </summary>
        /// <param name="objectKey">The object-key parameter.</param>
        /// <returns>The tags attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectTag> GetTags(ObjectKeyParameter objectKey)
        {
            ArgumentNullException.ThrowIfNull(objectKey);

            // through the object manager, so a key the caller may not read - by its grants
            // or its security level - has nothing to show
            var obj = CoreHub.ObjectManager.GetObjectByKey(objectKey.Value);
            if (obj is null)
            {
                return [];
            }

            return GetTags(obj.Id);
        }

        /// <summary>
        /// Returns every tag attached to the object with the supplied id, in chronological
        /// order (oldest first).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The tags attached to the object. The collection may be empty.</returns>
        public IEnumerable<ObjectTag> GetTags(Guid objectId)
        {
            var query = new Query<ObjectTag>()
                .WhereEquals(x => x.ObjectId, objectId);

            return ModelHub.GetObjectTags(query).OrderBy(t => t.Created).ToList();
        }

        /// <summary>
        /// Returns the tags that satisfy the supplied query. The manager opens its own
        /// DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching tags.</returns>
        public IEnumerable<ObjectTag> GetTags(IQuery<ObjectTag> query)
        {
            return ModelHub.GetObjectTags(query);
        }

        /// <summary>
        /// Returns the tags that satisfy the supplied query, executed inside the supplied
        /// <see cref="IQueryContext"/> (expected to be a <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching tags.</returns>
        public IEnumerable<ObjectTag> GetTags(IQuery<ObjectTag> query, IQueryContext context)
        {
            return ModelHub.GetObjectTags(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Attaches a tag with the supplied name and optional color to the object. When a tag
        /// of that name already exists on the object, the existing row is returned and
        /// <see cref="TagAdded"/> is NOT re-raised. Returns <see langword="null"/> when the
        /// object does not exist or the name is empty.
        /// </summary>
        /// <param name="objectId">The id of the object being tagged.</param>
        /// <param name="name">The tag display text.</param>
        /// <param name="color">The optional CSS color of the tag badge, or <c>null</c>.</param>
        /// <returns>The persisted tag, or <see langword="null"/>.</returns>
        public ObjectTag Add(Guid objectId, string name, string color)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            using var db = ModelHub.CreateDbContext();

            if (!db.Objects.AsNoTracking().Any(o => o.Id == objectId))
            {
                return null;
            }

            var existing = db.ObjectTags
                .AsNoTracking()
                .FirstOrDefault(t => t.ObjectId == objectId && t.Name == name);

            if (existing is not null)
            {
                return existing;
            }

            var tag = new ObjectTag
            {
                ObjectId = objectId,
                Name = name,
                Color = color,
                Created = DateTime.UtcNow
            };

            ModelHub.Add(tag);
            TagAdded?.Invoke(this, tag);
            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.tag.created", CoreHub.ObjectManager.GetObject(objectId));

            return tag;
        }

        /// <summary>
        /// Detaches the tag with the supplied id. Raises <see cref="TagRemoved"/> when a row
        /// existed.
        /// </summary>
        /// <param name="tagId">The id of the tag to remove.</param>
        /// <returns><see langword="true"/> when a row existed and was removed.</returns>
        public bool Remove(Guid tagId)
        {
            using var db = ModelHub.CreateDbContext();
            var existing = db.ObjectTags.FirstOrDefault(t => t.Id == tagId);

            if (existing is null)
            {
                return false;
            }

            ModelHub.Remove(existing);
            TagRemoved?.Invoke(this, existing);
            TryAddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.tag.deleted", CoreHub.ObjectManager.GetObject(existing.ObjectId));

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
        /// Emits a UI notification via <see cref="CoreHub.AddNotification"/>, swallowing any
        /// exception so that tests with a partially wired host don't crash.
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
