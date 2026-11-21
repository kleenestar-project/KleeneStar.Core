using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing objects, including adding, retrieving, and removing, as well as
    /// handling object-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing objects and events for tracking changes
    /// to the object collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public sealed class ObjectManager : IObjectManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an object is added.
        /// </summary>
        public event EventHandler<Model.Entities.Object> ObjectAdded;

        /// <summary>
        /// An event that fires when an object is udpated.
        /// </summary>
        public event EventHandler<Model.Entities.Object> ObjectUpdated;

        /// <summary>
        /// An event that fires when an object is removed.
        /// </summary>
        public event EventHandler<Model.Entities.Object> ObjectRemoved;

        /// <summary>
        /// An event that fires after an object was given another parent.
        /// </summary>
        public event EventHandler<ObjectMovedEventArgs> ObjectMoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ObjectManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a object based on its id.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The object.</returns>
        public Model.Entities.Object GetObject(Guid objectId)
        {
            var query = new Query<Model.Entities.Object>()
                .Where(x => x.Id == objectId)
                .WithPaging(0, 1);

            return GetObjects(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the first object that matches the specified key, using a 
        /// case-insensitive comparison.
        /// </summary>
        /// <param name="key">
        /// The key used to identify the object to retrieve. Must not be null, empty, or 
        /// consist only of whitespace.
        /// </param>
        /// <returns>
        /// The object associated with the specified key, or null if no matching object is 
        /// found or if the key is invalid.
        /// </returns>
        public Model.Entities.Object GetObjectByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var query = new Query<Model.Entities.Object>()
                .WhereEqualsIgnoreCase(x => x.Key, key)
                .WithPaging(0, 1);

            // a former key (the object moved to another workspace) still leads to it - read
            // narrowed like the key itself
            return GetObjects(query).FirstOrDefault()
                ?? (ModelHub.GetObjectIdByAlias(key.Trim()) is Guid moved ? GetObject(moved) : null);
        }

        /// <summary>
        /// Returns a object based on its key.
        /// </summary>
        /// <param name="objectKey">The key of the object.</param>
        /// <returns>The object.</returns>
        public Model.Entities.Object GetObjectByKey(ObjectKeyParameter objectKey)
        {
            var key = objectKey?.Value;

            return GetObjectByKey(key);
        }

        /// <summary>
        /// Returns the next free object key of a workspace, e.g. <c>SD-18</c>.
        /// </summary>
        /// <remarks>
        /// The key is the human-readable handle an object is addressed by, so it has to exist
        /// before the record is written, and no form asks for one. The next free number is
        /// derived from the keys already issued in the workspace. Two creates racing each other
        /// could derive the same number; the surrounding code is likewise single-writer, so the
        /// sequence is not guarded any further here.
        /// <para>
        /// It lives on the manager rather than at a call site because more than one caller
        /// creates objects the user never named - the create endpoint, and the setup a workspace
        /// template performs - and two implementations of the same numbering would eventually
        /// hand out the same key twice.
        /// </para>
        /// </remarks>
        /// <param name="workspaceId">The workspace the key is issued in.</param>
        /// <returns>The key, or <see langword="null"/> when the workspace is unknown or carries
        /// no key of its own to prefix with.</returns>
        public string NextObjectKey(Guid workspaceId)
        {
            var prefix = CoreHub.WorkspaceManager.GetWorkspace(workspaceId)?.Key;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                return null;
            }

            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId);
            var pattern = new Regex($"^{Regex.Escape(prefix)}-(\\d+)$", RegexOptions.IgnoreCase);

            // the numbering has to see the keys of the classified objects too - a key derived
            // from what the caller happens to be cleared for would be handed out twice
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            // a key given up by an object that moved to another workspace stays its alias, so it
            // is never handed out again (transfer)
            var next = GetObjects(query)
                .Select(x => x.Key)
                .Concat(ModelHub.GetAliasKeysWithPrefix(prefix))
                .Select(x => pattern.Match(x ?? string.Empty))
                .Where(m => m.Success)
                .Select(m => int.TryParse(m.Groups[1].Value, out var number) ? number : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            return $"{prefix}-{next.ToString(CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Retrieves a collection of objects that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned objects. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of objects that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Model.Entities.Object> GetObjects(IQuery<Model.Entities.Object> query)
        {
            return ModelHub.GetObjects(Restrict(query));
        }

        /// <summary>
        /// Returns how many objects satisfy the specified filter criteria without loading
        /// them.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted objects. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching objects.</returns>
        public int CountObjects(IQuery<Model.Entities.Object> query)
        {
            return ModelHub.CountObjects(Restrict(query));
        }

        /// <summary>
        /// Returns the ids of the classes the objects that satisfy the supplied filter criteria
        /// belong to, without loading the objects.
        /// </summary>
        /// <param name="query">The query criteria. Paging must be left off.</param>
        /// <returns>The distinct class ids.</returns>
        public IReadOnlyList<Guid> GetClassIds(IQuery<Model.Entities.Object> query)
        {
            return ModelHub.GetObjectClassIds(Restrict(query));
        }

        /// <summary>
        /// Retrieves a collection of objects that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned objects. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of objects that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Model.Entities.Object> GetObjects(IQuery<Model.Entities.Object> query, IQueryContext context)
        {
            return ModelHub.GetObjects(Restrict(query), context as KleeneStarDbContext);
        }

        /// <summary>
        /// Narrows an object query to the classifications the identity behind the current
        /// request is cleared for.
        /// </summary>
        /// <remarks>
        /// Every read this manager performs on a user's behalf passes through here, which is
        /// what makes "an object nobody may see does not appear" a property of the system
        /// rather than of the lists somebody remembered to guard. The reads the system performs
        /// on its own behalf - issuing the next object key, a relation guard, a commit replay -
        /// open an unrestricted scope and are handed the query untouched.
        /// <para>
        /// The narrowing is a predicate rather than a filter over the result, so it lands
        /// before paging and a page of hidden records does not come back short.
        /// </para>
        /// <para>
        /// The same predicate carries the permissions: an object of a class whose chain does not
        /// grant the caller <c>ObjectReadPermission</c> is not answered either
        /// (<see cref="WebPermission.ContentVisibility"/>), lifted by the same unrestricted
        /// scope.
        /// </para>
        /// </remarks>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query.</returns>
        private static IQuery<Model.Entities.Object> Restrict(IQuery<Model.Entities.Object> query)
        {
            query = WebPermission.ContentVisibility.Restrict(query);

            var securityLevelManager = CoreHub.SecurityLevelManager;

            if (securityLevelManager is null)
            {
                return query;
            }

            // the request is not threaded through the manager, and the session manager answers
            // the current identity without one
            var identityId = CoreHub.SessionManager?.GetCurrentIdentityId(null) ?? Guid.Empty;

            return securityLevelManager.Restrict(query, identityId);
        }

        /// <summary>
        /// Returns the active objects the supplied identity has most recently opened, newest
        /// first, capped at <paramref name="count"/>. Backs the "recently used" section of the
        /// object dropdown in the application header.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="count">The maximum number of objects to return.</param>
        /// <returns>The recently opened objects, newest first. The collection may be empty.</returns>
        public IReadOnlyList<Model.Entities.Object> GetRecentObjects(Guid ownerId, int count)
        {
            return GetRecentObjects(ownerId, count, null);
        }

        /// <summary>
        /// Returns the active objects of the supplied kind the supplied identity has most
        /// recently opened, newest first, capped at <paramref name="count"/>. Backs the
        /// "recently used" section of a per-kind dropdown in the application header.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="count">The maximum number of objects to return.</param>
        /// <param name="kind">
        /// The kind key to filter by. Null, empty, or whitespace returns objects of every
        /// kind. The key is normalized (trimmed, lower-cased) before the comparison so it
        /// matches the persisted <see cref="Model.Entities.Object.Kind"/>.
        /// </param>
        /// <returns>The recently opened objects of the kind, newest first. The collection may be empty.</returns>
        public IReadOnlyList<Model.Entities.Object> GetRecentObjects(Guid ownerId, int count, string kind)
        {
            var normalized = string.IsNullOrWhiteSpace(kind)
                ? null
                : Model.Entities.ObjectKind.Normalize(kind);

            // the visits are the owner's own, so the classification is judged against the
            // owner rather than against whoever the ambient request belongs to. It is filtered
            // after the take-count on purpose: a record the owner may no longer see should
            // leave the list short rather than pull an older one up into it
            return [.. ModelHub.GetObjectVisits(new Query<Model.Entities.ObjectVisit>())
                .Where(x => x.OwnerId == ownerId
                    && x.LastVisited != default
                    && x.Object is not null
                    && x.Object.State == Model.Entities.WorkspaceState.Active
                    && (normalized == null || x.Object.Kind == normalized))
                .OrderByDescending(x => x.LastVisited)
                .Take(Math.Max(0, count))
                .Select(x => x.Object)
                .Where(x => CoreHub.SecurityLevelManager?.IsCleared(ownerId, x.SecurityLevelId) != false)];
        }

        /// <summary>
        /// Records that the supplied identity has just opened the supplied object by advancing the
        /// visit's last-visited timestamp (inserting the visit when needed). The mutation is
        /// deliberately quiet because it fires on every object detail page load. Returns
        /// <see langword="null"/> when the owner or object does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The persisted visit, or <see langword="null"/>.</returns>
        public Model.Entities.ObjectVisit RecordVisit(Guid ownerId, Guid objectId)
        {
            return ModelHub.UpsertObjectVisit(ownerId, objectId, favorite: null, recordVisit: true);
        }

        /// <summary>
        /// Returns the active objects the supplied identity has starred, ordered by key.
        /// Backs the "starred" quickfilter of the issues overview.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <returns>The starred objects. The collection may be empty.</returns>
        public IReadOnlyList<Model.Entities.Object> GetFavoriteObjects(Guid ownerId)
        {
            return [.. ModelHub.GetObjectVisits(new Query<Model.Entities.ObjectVisit>())
                .Where(x => x.OwnerId == ownerId
                    && x.Favorite
                    && x.Object is not null
                    && x.Object.State == Model.Entities.WorkspaceState.Active)
                .OrderBy(x => x.Object.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Object)];
        }

        /// <summary>
        /// Returns whether the supplied identity has starred the supplied object.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the object is starred by the identity.</returns>
        public bool IsFavorite(Guid ownerId, Guid objectId)
        {
            return ModelHub.GetObjectVisit(ownerId, objectId)?.Favorite ?? false;
        }

        /// <summary>
        /// Returns whether the supplied identity has liked the supplied object.
        /// </summary>
        /// <param name="ownerId">The id of the liking identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the identity has liked it.</returns>
        public bool IsLiked(Guid ownerId, Guid objectId)
        {
            return ModelHub.GetObjectVisit(ownerId, objectId)?.Liked ?? false;
        }

        /// <summary>
        /// Returns whether the supplied identity has opened the supplied object before.
        /// </summary>
        /// <param name="ownerId">The id of the reading identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the object has been opened before.</returns>
        public bool IsRead(Guid ownerId, Guid objectId)
        {
            // a row exists as soon as anything about the object was recorded for this identity -
            // a star, a like - so the timestamp is what says it was opened, not the row
            return ModelHub.GetObjectVisit(ownerId, objectId)?.LastVisited > default(DateTime);
        }

        /// <summary>
        /// Returns how many identities have liked the supplied object.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The number of likes.</returns>
        public int GetLikeCount(Guid objectId)
        {
            // the object narrows the query and the flag is counted here: a visit row exists per
            // identity that ever touched the object, which is a handful, and the flag is not one
            // of the comparisons the index query offers
            var query = new Query<Model.Entities.ObjectVisit>()
                .WhereEquals(x => x.ObjectId, objectId);

            return ModelHub.GetObjectVisits(query).Count(x => x.Liked);
        }

        /// <summary>
        /// Sets the liked state of the supplied object for the supplied identity.
        /// </summary>
        /// <remarks>
        /// No commit and no audit event: what a reader thought of a post is not a revision of it.
        /// The star does raise an update, because a starred object changes what its owner's lists
        /// show; a like changes only a number under the post.
        /// </remarks>
        /// <param name="ownerId">The id of the liking identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="liked">The new liked state.</param>
        /// <returns>The persisted visit row, or <see langword="null"/>.</returns>
        public Model.Entities.ObjectVisit SetLike(Guid ownerId, Guid objectId, bool liked)
        {
            return ModelHub.UpsertObjectVisit(ownerId, objectId, favorite: null, recordVisit: false, liked: liked);
        }

        /// <summary>
        /// Sets the starred state of the supplied object for the supplied identity, inserting
        /// or updating the backing visit row. Returns <see langword="null"/> when the owner or
        /// object does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="favorite">The new starred state.</param>
        /// <returns>The persisted visit row, or <see langword="null"/>.</returns>
        public Model.Entities.ObjectVisit SetFavorite(Guid ownerId, Guid objectId, bool favorite)
        {
            var visit = ModelHub.UpsertObjectVisit(ownerId, objectId, favorite, recordVisit: false);

            if (visit is not null)
            {
                var @object = GetObject(objectId);

                ObjectUpdated?.Invoke(this, @object);

                // confirmation toast (pushed over the message queue; harmless when the host is not wired)
                CoreHub.AddNotification
                (
                    favorite ? "kleenestar.core:notification.title.favorited" : "kleenestar.core:notification.title.unfavorited",
                    favorite ? "kleenestar.core:notification.object.favorited" : "kleenestar.core:notification.object.unfavorited",
                    @object
                );
            }

            return visit;
        }

        /// <summary>
        /// The reason a write is refused that would put the home page of a workspace into its
        /// page tree.
        /// </summary>
        internal const string HomeOutsideTree = "The home page of a workspace stands outside its page tree.";

        /// <summary>
        /// Adds a object to the manager.
        /// </summary>
        /// <remarks>
        /// The genesis commit of the object's history is opened here, so an object cannot come
        /// into existence without one. The scope joins whatever the caller already opened, which
        /// is how the field values written straight after a create end up in the same genesis
        /// commit instead of in a follow-up edit.
        /// </remarks>
        /// <param name="objectEntity">The object to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IObjectManager Add(Model.Entities.Object objectEntity)
        {
            ArgumentNullException.ThrowIfNull(objectEntity);

            objectEntity.Kind = DeriveKind(objectEntity);

            var parent = objectEntity.ParentId is Guid parentId ? ObjectHierarchy.Read(parentId) : null;

            // the home page is not part of the page tree: no object is created below it
            if (parent is not null && CoreHub.WorkspaceManager?.IsHome(parent) == true)
            {
                throw new InvalidOperationException(HomeOutsideTree);
            }

            // a new object below a parent at the deepest level would sit past the limit
            if (parent is not null)
            {
                MeasureDepth(objectEntity.Id, parent, (CoreHub.HierarchySettings ?? new HierarchySettings()).EffectiveMaxDepth);
            }

            using var scope = CoreHub.CommitManager.BeginCommit
            (
                objectEntity.Id,
                CommitType.Created,
                objectEntity.CreatorId ?? Guid.Empty
            );

            ModelHub.Add(objectEntity);

            // the derived path index follows every parent write (phase 3)
            ObjectPathIndex.Added(objectEntity);

            RecordProperties(objectEntity.Id, null, objectEntity);

            ObjectAdded?.Invoke(this, objectEntity);

            // the other open pages reload their lists (folded into one inside a batch)
            ObjectChangeNotices.Notify(objectEntity.Id);

            // create notification. The key and the link are what turn the entry in the
            // notification center from "an object was created" into one the user can act on.
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.object.created", objectEntity);

            return this;
        }

        /// <summary>
        /// Update a object to the manager.
        /// </summary>
        /// <remarks>
        /// The persisted row is read before it is overwritten so the commit can record what each
        /// system property was as well as what it became. An update that changes nothing writes
        /// no commit — the scope closes empty.
        /// </remarks>
        /// <param name="objectEntity">The object to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IObjectManager Update(Model.Entities.Object objectEntity)
        {
            ArgumentNullException.ThrowIfNull(objectEntity);

            objectEntity.Kind = DeriveKind(objectEntity);

            var previous = GetObject(objectEntity.Id);

            var previousParentId = (previous ?? ObjectHierarchy.Read(objectEntity.Id))?.ParentId;

            // a parent changed through an update follows the same rule as a move: the home page
            // has no parent and no children
            if (previousParentId != objectEntity.ParentId && objectEntity.ParentId is Guid newParentId)
            {
                var newParent = ObjectHierarchy.Read(newParentId);

                if (CoreHub.WorkspaceManager?.IsHome(objectEntity) == true
                    || (newParent is not null && CoreHub.WorkspaceManager?.IsHome(newParent) == true))
                {
                    throw new InvalidOperationException(HomeOutsideTree);
                }
            }

            using var scope = CoreHub.CommitManager.BeginCommit
            (
                objectEntity.Id,
                CommitType.Updated,
                objectEntity.UpdaterId ?? Guid.Empty
            );

            ModelHub.Update(objectEntity);

            // a changed parent moves the object's row and its subtree's in the path index
            if (previousParentId != objectEntity.ParentId)
            {
                ObjectPathIndex.Moved(objectEntity);
            }

            RecordProperties(objectEntity.Id, previous, objectEntity);

            ObjectUpdated?.Invoke(this, objectEntity);

            ObjectChangeNotices.Notify(objectEntity.Id);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.object.updated", objectEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified object from the manager.
        /// </summary>
        /// <remarks>
        /// The terminal <see cref="CommitType.Deleted"/> commit is appended before the row is
        /// dropped, while the key and the field values can still be read. The chain itself is
        /// not deleted with the object: what an object contained before it was removed is the
        /// part of the history an audit most often asks for. It is attributed to the identity
        /// acting on the call chain - it used to name the object's last editor.
        /// <para>
        /// An object that still has children is refused <em>before</em> the commit is written.
        /// The parent relation is a restricting foreign key, so the database would refuse the
        /// row anyway - but only after the terminal commit had already claimed a deletion that
        /// never happened. The children are counted without narrowing: a child the caller cannot
        /// see still holds on to its parent.
        /// </para>
        /// </remarks>
        /// <param name="objectId">The object id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        /// <exception cref="HierarchyConflictException">The object still has children.</exception>
        public IObjectManager Remove(Guid objectId)
        {
            var objectEntry = GetObject(objectId);

            if (objectEntry is not null)
            {
                var children = ModelHub.CountObjects(new Query<Model.Entities.Object>()
                    .Where(x => x.ParentId == objectId));

                if (children > 0)
                {
                    throw new HierarchyConflictException
                    (
                        $"The object still contains {children.ToString(CultureInfo.InvariantCulture)} object(s); move or remove them first.",
                        children
                    );
                }

                // the deletion is the act of whoever is acting now, not of the object's last
                // editor; nobody acting (a compensation, a repair) records it without an author,
                // just as the audit log names the actor from the session
                var actor = CoreHub.SessionManager?.GetCurrentIdentityId(null) ?? Guid.Empty;

                CoreHub.CommitManager.AddCommit(objectId, CommitType.Deleted, [], actor);

                ModelHub.Remove(objectEntry);
                ObjectRemoved?.Invoke(this, objectEntry);

                ObjectChangeNotices.Notify(objectEntry.Id);
            }

            return this;
        }

        /// <summary>
        /// Reports the system properties that differ between two revisions of an object to the
        /// open commit scope.
        /// </summary>
        /// <remarks>
        /// Half of what a user changes on an object lives on the object row rather than in a
        /// value row — the summary, the assignee, where it sits in the hierarchy. Recording only
        /// the class fields would leave those changes out of the history entirely.
        /// </remarks>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="previous">The object as it was persisted, or <c>null</c> for a creation.</param>
        /// <param name="current">The object as it is now.</param>
        private static void RecordProperties(Guid objectId, Model.Entities.Object previous, Model.Entities.Object current)
        {
            foreach (var name in ObjectProperty.All)
            {
                var before = ObjectProperty.Read(previous, name);
                var after = ObjectProperty.Read(current, name);

                if (string.Equals(before, after, StringComparison.Ordinal))
                {
                    continue;
                }

                CoreHub.CommitManager.Record
                (
                    objectId,
                    new Change
                    {
                        Name = name,
                        OldValue = before,
                        NewValue = after
                    },
                    CommitType.Updated,
                    current.UpdaterId ?? current.CreatorId ?? Guid.Empty
                );
            }
        }

        /// <summary>
        /// Derives the kind of the supplied object from its class — the class is the
        /// single source of the kind. When the class cannot be resolved (e.g. in
        /// isolated tests), the object's own kind is kept, normalized.
        /// </summary>
        /// <param name="objectEntity">The object whose kind is derived. Cannot be null.</param>
        /// <returns>The normalized kind key. Never null or empty.</returns>
        private static string DeriveKind(Model.Entities.Object objectEntity)
        {
            var classEntity = CoreHub.ClassManager.GetClass(objectEntity.ClassId);

            return Model.Entities.ObjectKind.Normalize(classEntity?.Kind ?? objectEntity.Kind);
        }

        /// <summary>
        /// Returns the parent of the specified object, or <c>null</c> when none is set.
        /// </summary>
        /// <param name="objectId">The id of the child object.</param>
        public Model.Entities.Object GetParent(Guid objectId)
        {
            var child = GetObject(objectId);

            if (child?.ParentId is null)
            {
                return null;
            }

            return GetObject(child.ParentId.Value);
        }

        /// <summary>
        /// Returns the immediate children of the specified object.
        /// </summary>
        /// <param name="objectId">The id of the parent object.</param>
        public IEnumerable<Model.Entities.Object> GetChildren(Guid objectId)
        {
            var query = new Query<Model.Entities.Object>()
                .Where(x => x.ParentId == objectId);

            return ModelHub.GetObjects(query);
        }

        /// <summary>
        /// Returns the siblings of the specified object: every other object inside the
        /// same workspace and class. The supplied object itself is excluded.
        /// </summary>
        /// <param name="objectId">The id of the reference object.</param>
        public IEnumerable<Model.Entities.Object> GetSiblings(Guid objectId)
        {
            var reference = GetObject(objectId);

            if (reference is null)
            {
                return [];
            }

            var query = new Query<Model.Entities.Object>()
                .Where(x => x.WorkspaceId == reference.WorkspaceId
                         && x.ClassId == reference.ClassId
                         && x.Id != objectId);

            return ModelHub.GetObjects(query);
        }

        /// <summary>
        /// Returns the ancestor chain of the specified object, nearest first (parent,
        /// grandparent, …, root). The walk keeps a visited set so a cycle persisted by
        /// older data terminates instead of looping forever.
        /// </summary>
        /// <param name="objectId">The id of the object whose ancestors are resolved.</param>
        /// <returns>The ancestors, nearest first. The collection may be empty.</returns>
        public IEnumerable<Model.Entities.Object> GetAncestors(Guid objectId)
        {
            var ancestors = new List<Model.Entities.Object>();
            var visited = new HashSet<Guid> { objectId };

            var current = GetObject(objectId);

            while (current?.ParentId is not null && visited.Add(current.ParentId.Value))
            {
                current = GetObject(current.ParentId.Value);

                if (current is null)
                {
                    break;
                }

                ancestors.Add(current);
            }

            return ancestors;
        }

        /// <summary>
        /// Returns every descendant of the specified object (children, grandchildren, …)
        /// in breadth-first order. The traversal keeps a visited set so a cycle persisted
        /// by older data terminates instead of looping forever.
        /// </summary>
        /// <param name="objectId">The id of the object whose subtree is resolved.</param>
        /// <returns>The descendants in breadth-first order. The collection may be empty.</returns>
        public IEnumerable<Model.Entities.Object> GetDescendants(Guid objectId)
        {
            var descendants = new List<Model.Entities.Object>();
            var visited = new HashSet<Guid> { objectId };
            var frontier = new Queue<Guid>();
            frontier.Enqueue(objectId);

            while (frontier.Count > 0)
            {
                foreach (var child in GetChildren(frontier.Dequeue()))
                {
                    if (!visited.Add(child.Id))
                    {
                        continue;
                    }

                    descendants.Add(child);
                    frontier.Enqueue(child.Id);
                }
            }

            return descendants;
        }

        /// <summary>
        /// Sets or clears the parent of the specified object after validating the
        /// hierarchy rules: the parent must exist, must not be the object itself, must
        /// not be one of the object's descendants (no cycles), must live in the same
        /// workspace, must not push any object of the moved subtree below
        /// <see cref="HierarchySettings.MaxDepth"/>, and — when the parent's class declares
        /// allowed child classes — the object's class must be among them. An empty
        /// <see cref="Model.Entities.Class.AllowedChildren"/> list is treated as
        /// "composition not restricted" so existing data stays linkable.
        /// </summary>
        /// <param name="objectId">The id of the object whose parent is set.</param>
        /// <param name="parentId">The id of the new parent, or <c>null</c> to detach.</param>
        /// <returns>
        /// The updated object, or <c>null</c> when no object with the supplied id exists.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when one of the hierarchy rules is violated.
        /// </exception>
        /// <exception cref="HierarchyConflictException">
        /// Thrown when another move of the workspace held the hierarchy lock for too long.
        /// </exception>
        public Model.Entities.Object SetParent(Guid objectId, Guid? parentId)
        {
            return Move(objectId, parentId, expect: false, expectedParentId: null);
        }

        /// <summary>
        /// Sets or clears the parent of the specified object like
        /// <see cref="SetParent(Guid, Guid?)"/>, but only while the object still has the
        /// parent the caller saw.
        /// </summary>
        /// <param name="objectId">The id of the object whose parent is set.</param>
        /// <param name="parentId">The id of the new parent, or <c>null</c> to detach.</param>
        /// <param name="expectedParentId">
        /// The parent the caller believes the object has, or <c>null</c> for the root.
        /// </param>
        /// <returns>
        /// The updated object, or <c>null</c> when no object with the supplied id exists.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when one of the hierarchy rules is violated.
        /// </exception>
        /// <exception cref="HierarchyConflictException">
        /// Thrown when the object no longer has the expected parent, or when another move of
        /// the workspace held the hierarchy lock for too long.
        /// </exception>
        public Model.Entities.Object SetParent(Guid objectId, Guid? parentId, Guid? expectedParentId)
        {
            return Move(objectId, parentId, expect: true, expectedParentId);
        }

        /// <summary>
        /// Carries out a move: validates it and writes it under the hierarchy lock of the
        /// object's workspace, so the tree the rules are checked against is the tree the move
        /// is written into.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The cycle check walks up, not down.</b> "Is the new parent inside the moved
        /// subtree?" is the same question as "is the moved object an ancestor of the new
        /// parent?", and the second one is answered by following the parent links of the
        /// target to the root - at most <see cref="HierarchySettings.MaxDepth"/> reads -
        /// instead of reading the whole subtree of the moved object, one query per node.
        /// </para>
        /// <para>
        /// The walks read <b>without narrowing</b>: an ancestor or a descendant the caller
        /// cannot see is still part of the tree, and a cycle through a classified object is
        /// still a cycle. Nothing the walks read reaches the caller beyond the refusal. The
        /// object and the new parent themselves are read narrowed, so a caller cannot move
        /// what it does not see, nor into what it does not see.
        /// </para>
        /// </remarks>
        /// <param name="objectId">The id of the object whose parent is set.</param>
        /// <param name="parentId">The id of the new parent, or <c>null</c> to detach.</param>
        /// <param name="expect">Whether <paramref name="expectedParentId"/> is checked.</param>
        /// <param name="expectedParentId">The parent the caller believes the object has.</param>
        /// <returns>The updated object, or <c>null</c> when no object with the id exists.</returns>
        private Model.Entities.Object Move(Guid objectId, Guid? parentId, bool expect, Guid? expectedParentId)
        {
            var objectEntity = GetObject(objectId);

            if (objectEntity is null)
            {
                return null;
            }

            using var hierarchy = HierarchyLock.Enter(objectEntity.WorkspaceId);

            // read again under the lock: a move that finished while this one waited has
            // changed the state the checks below must see
            objectEntity = GetObject(objectId);

            if (objectEntity is null)
            {
                return null;
            }

            if (expect && objectEntity.ParentId != expectedParentId)
            {
                throw new HierarchyConflictException("The object was moved by somebody else in the meantime; reload and try again.");
            }

            var previousParentId = objectEntity.ParentId;

            if (parentId is null)
            {
                if (previousParentId is null)
                {
                    return objectEntity;
                }

                objectEntity.ParentId = null;
                StampMove(objectEntity);
                Update(objectEntity);

                ObjectMoved?.Invoke(this, new ObjectMovedEventArgs(objectEntity, previousParentId, null));

                return objectEntity;
            }

            if (parentId.Value == objectId)
            {
                throw new InvalidOperationException("An object cannot be its own parent.");
            }

            var parent = GetObject(parentId.Value);

            if (parent is null)
            {
                throw new InvalidOperationException($"The parent object '{parentId}' does not exist.");
            }

            if (parent.WorkspaceId != objectEntity.WorkspaceId)
            {
                throw new InvalidOperationException("Parent and child must belong to the same workspace.");
            }

            // the home page is not part of the page tree: it has no parent and no children
            if (CoreHub.WorkspaceManager?.IsHome(parent) == true || CoreHub.WorkspaceManager?.IsHome(objectEntity) == true)
            {
                throw new InvalidOperationException(HomeOutsideTree);
            }

            var maxDepth =(CoreHub.HierarchySettings ?? new HierarchySettings()).EffectiveMaxDepth;
            var parentDepth = MeasureDepth(objectId, parent, maxDepth);

            // the moved object lands one level below its parent, and the deepest object of its
            // subtree as many levels below that as the subtree is high
            if (parentDepth + 1 + MeasureHeight(objectId, maxDepth - parentDepth - 1) > maxDepth)
            {
                throw new InvalidOperationException(
                    $"The move would place objects deeper than {maxDepth.ToString(CultureInfo.InvariantCulture)} levels.");
            }

            var parentClass = CoreHub.ClassManager.GetClass(parent.ClassId);

            if (parentClass?.AllowedChildren is { Count: > 0 }
                && parentClass.AllowedChildren.All(c => c.Id != objectEntity.ClassId))
            {
                throw new InvalidOperationException(
                    $"Objects of this class are not allowed beneath '{parentClass.Name}' (see the class's allowed children).");
            }

            if (previousParentId == parentId)
            {
                return objectEntity;
            }

            objectEntity.ParentId = parentId;
            StampMove(objectEntity);
            Update(objectEntity);

            ObjectMoved?.Invoke(this, new ObjectMovedEventArgs(objectEntity, previousParentId, parentId));

            return objectEntity;
        }

        /// <summary>
        /// Stamps a move onto the object: the time, and the identity acting on this call chain
        /// as its updater.
        /// </summary>
        /// <remarks>
        /// The commit of an update is attributed to <see cref="Model.Entities.Object.UpdaterId"/>,
        /// and a move used to leave it as it was - so the move was recorded under whoever had
        /// edited the object last, a real person's name under somebody else's act. Nobody acting
        /// (the system, a repair) clears it instead: <see cref="Guid.Empty"/> is never written
        /// into an identity reference.
        /// </remarks>
        /// <param name="objectEntity">The object being moved.</param>
        private static void StampMove(Model.Entities.Object objectEntity)
        {
            var actor = CoreHub.SessionManager?.GetCurrentIdentityId(null) ?? Guid.Empty;

            objectEntity.UpdaterId = actor == Guid.Empty ? null : actor;
            objectEntity.Updated = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns the level the supplied parent sits at (a root object is level one) and
        /// refuses when the moved object is among its ancestors.
        /// </summary>
        /// <param name="objectId">The id of the object being moved.</param>
        /// <param name="parent">The prospective parent.</param>
        /// <param name="maxDepth">The deepest permitted level.</param>
        /// <returns>The level of <paramref name="parent"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// The move would close a cycle, the parent already sits at the deepest level, or the
        /// parent's own ancestry is circular.
        /// </exception>
        private int MeasureDepth(Guid objectId, Model.Entities.Object parent, int maxDepth)
        {
            // the path index answers without a walk: the parent's path names every ancestor
            if (ObjectPathIndex.Ancestors(parent.Id) is { } ancestors)
            {
                if (parent.Id == objectId || ancestors.Contains(objectId))
                {
                    throw new InvalidOperationException("The chosen parent is a descendant of the object; the link would create a cycle.");
                }

                var level = ancestors.Count + 1;

                if (level >= maxDepth)
                {
                    throw new InvalidOperationException(
                        $"The chosen parent already sits at the deepest permitted level ({maxDepth.ToString(CultureInfo.InvariantCulture)}).");
                }

                return level;
            }

            // the system asks: the ancestry is the tree's, not what the caller is cleared for
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            var visited = new HashSet<Guid>();
            var current = parent;
            var depth = 0;

            while (current is not null)
            {
                if (current.Id == objectId)
                {
                    throw new InvalidOperationException("The chosen parent is a descendant of the object; the link would create a cycle.");
                }

                if (!visited.Add(current.Id))
                {
                    throw new InvalidOperationException("The ancestry of the chosen parent is circular; the hierarchy needs repair.");
                }

                if (++depth >= maxDepth)
                {
                    throw new InvalidOperationException(
                        $"The chosen parent already sits at the deepest permitted level ({maxDepth.ToString(CultureInfo.InvariantCulture)}).");
                }

                current = current.ParentId is Guid next ? GetObject(next) : null;
            }

            return depth;
        }

        /// <summary>
        /// Returns how many levels the subtree below the supplied object reaches (zero for an
        /// object without children), reading at most one level more than
        /// <paramref name="limit"/> - a subtree deeper than that is refused anyway, and how
        /// much deeper does not matter.
        /// </summary>
        /// <param name="objectId">The root of the subtree.</param>
        /// <param name="limit">The number of levels the subtree may reach.</param>
        /// <returns>The height, or <paramref name="limit"/> + 1 when it exceeds the limit.</returns>
        private static int MeasureHeight(Guid objectId, int limit)
        {
            // one query over the path range when the index is complete
            if (ObjectPathIndex.Height(objectId) is int height_)
            {
                return Math.Min(height_, limit + 1);
            }

            var frontier = new List<Guid> { objectId };
            var visited = new HashSet<Guid> { objectId };
            var height = 0;

            while (height <= limit)
            {
                var ids = frontier;

                // unrestricted on purpose (ModelHub directly): a hidden grandchild still
                // occupies its level
                frontier = [.. ModelHub.GetObjects(new Query<Model.Entities.Object>()
                        .Where(x => x.ParentId != null && ids.Contains(x.ParentId.Value)))
                    .Select(x => x.Id)
                    .Where(visited.Add)];

                if (frontier.Count == 0)
                {
                    return height;
                }

                height++;
            }

            return height;
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
