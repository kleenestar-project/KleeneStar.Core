using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing objects, including adding, retrieving, and removing, as well as
    /// handling object-related events.
    /// </summary>
    /// <remarks>
    /// <b>Every read is classified.</b> The retrieval methods answer only what the identity
    /// behind the current request is cleared to see: an object carrying a security level its
    /// class defines is absent from the result unless the identity belongs to one of the groups
    /// the level names. An unclassified object is answered to everyone. The rule is applied
    /// here rather than at the call sites so a list written tomorrow obeys it without its
    /// author having to remember to; a read the system performs on its own behalf lifts it with
    /// <see cref="ISecurityLevelManager.BeginUnrestricted"/> and says why.
    /// </remarks>
    public interface IObjectManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an object is added.
        /// </summary>
        event System.EventHandler<Object> ObjectAdded;

        /// <summary>
        /// An event that fires when an object is udpated.
        /// </summary>
        event System.EventHandler<Object> ObjectUpdated;

        /// <summary>
        /// An event that fires when an object is removed.
        /// </summary>
        event System.EventHandler<Object> ObjectRemoved;

        /// <summary>
        /// An event that fires after an object was given another parent (or was detached to the
        /// root of its workspace).
        /// </summary>
        /// <remarks>
        /// It fires in addition to <see cref="ObjectUpdated"/>, after the move was written and
        /// recorded in the history, and only when the parent actually changed. It carries both
        /// ends, which an update does not: a listener reacting to content arriving in a place
        /// (automation, watchers, a live tree) needs to know where the object came from too.
        /// </remarks>
        event System.EventHandler<ObjectMovedEventArgs> ObjectMoved;

        /// <summary>
        /// Returns a object based on its id.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The object.</returns>
        Object GetObject(System.Guid objectId);

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
        Object GetObjectByKey(string key);

        /// <summary>
        /// Returns a object based on its key.
        /// </summary>
        /// <param name="objectKey">The key of the object.</param>
        /// <returns>The object.</returns>
        Object GetObjectByKey(ObjectKeyParameter objectKey);

        /// <summary>
        /// Returns the next free object key of a workspace, e.g. <c>SD-18</c>, derived from the
        /// keys already issued there.
        /// </summary>
        /// <remarks>
        /// Every caller that creates an object the user never named needs this - the create
        /// endpoint and the setup a workspace template performs - and two implementations of the
        /// same numbering would eventually hand out the same key twice.
        /// </remarks>
        /// <param name="workspaceId">The workspace the key is issued in.</param>
        /// <returns>The key, or <see langword="null"/> when the workspace is unknown or carries
        /// no key of its own to prefix with.</returns>
        /// <remarks>
        /// The type is written out because this file deliberately does not import
        /// <c>System</c>: it names <see cref="Object"/> throughout, and that would become
        /// ambiguous with <see cref="System.Object"/>.
        /// </remarks>
        string NextObjectKey(System.Guid workspaceId);

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
        IEnumerable<Object> GetObjects(IQuery<Object> query);

        /// <summary>
        /// Retrieves a collection of bojects that satisfy the specified filter criteria.
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
        IEnumerable<Object> GetObjects(IQuery<Object> query, IQueryContext context);

        /// <summary>
        /// Returns how many objects satisfy the supplied filter criteria without loading
        /// them. This is what a headline figure such as the landing page's issue count asks
        /// for - the number alone, never the rows behind it.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted objects. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching objects.</returns>
        int CountObjects(IQuery<Object> query);

        /// <summary>
        /// Returns the ids of the classes the objects that satisfy the supplied filter criteria
        /// belong to, without loading the objects. Narrowed like
        /// <see cref="GetObjects(IQuery{Object})"/>, so a class is answered only for objects the
        /// caller may read.
        /// </summary>
        /// <param name="query">The query criteria. Paging must be left off.</param>
        /// <returns>The distinct class ids.</returns>
        IReadOnlyList<System.Guid> GetClassIds(IQuery<Object> query);

        /// <summary>
        /// Returns the active objects the supplied identity has most recently opened, newest
        /// first, capped at <paramref name="count"/>. Backs the "recently used" section of the
        /// object dropdown in the application header.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="count">The maximum number of objects to return.</param>
        /// <returns>The recently opened objects, newest first. The collection may be empty.</returns>
        IReadOnlyList<Object> GetRecentObjects(System.Guid ownerId, int count);

        /// <summary>
        /// Returns the active objects of the supplied kind the supplied identity has most
        /// recently opened, newest first, capped at <paramref name="count"/>. Backs the
        /// "recently used" section of a per-kind dropdown in the application header.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="count">The maximum number of objects to return.</param>
        /// <param name="kind">
        /// The kind key to filter by. Null, empty, or whitespace returns objects of every
        /// kind (equivalent to <see cref="GetRecentObjects(System.Guid, int)"/>).
        /// </param>
        /// <returns>The recently opened objects of the kind, newest first. The collection may be empty.</returns>
        IReadOnlyList<Object> GetRecentObjects(System.Guid ownerId, int count, string kind);

        /// <summary>
        /// Records that the supplied identity has just opened the supplied object by advancing the
        /// visit's last-visited timestamp (inserting the visit when needed). The mutation is
        /// deliberately quiet because it fires on every object detail page load. Returns
        /// <see langword="null"/> when the owner or object does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The persisted visit, or <see langword="null"/>.</returns>
        ObjectVisit RecordVisit(System.Guid ownerId, System.Guid objectId);

        /// <summary>
        /// Returns the active objects the supplied identity has starred, ordered by key.
        /// Backs the "starred" quickfilter of the issues overview.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <returns>The starred objects. The collection may be empty.</returns>
        IReadOnlyList<Object> GetFavoriteObjects(System.Guid ownerId);

        /// <summary>
        /// Returns whether the supplied identity has starred the supplied object.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the object is starred by the identity.</returns>
        bool IsFavorite(System.Guid ownerId, System.Guid objectId);

        /// <summary>
        /// Sets the starred state of the supplied object for the supplied identity, inserting
        /// or updating the backing visit row. Returns <see langword="null"/> when the owner or
        /// object does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="favorite">The new starred state.</param>
        /// <returns>The persisted visit row, or <see langword="null"/>.</returns>
        ObjectVisit SetFavorite(System.Guid ownerId, System.Guid objectId, bool favorite);

        /// <summary>
        /// Returns whether the supplied identity has liked the supplied object.
        /// </summary>
        /// <param name="ownerId">The id of the liking identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the identity has liked it.</returns>
        bool IsLiked(System.Guid ownerId, System.Guid objectId);

        /// <summary>
        /// Returns how many identities have liked the supplied object.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The number of likes.</returns>
        int GetLikeCount(System.Guid objectId);

        /// <summary>
        /// Sets the liked state of the supplied object for the supplied identity. Returns
        /// <see langword="null"/> when the owner or object does not exist.
        /// </summary>
        /// <remarks>
        /// A like is public and a star is private, so unlike <see cref="SetFavorite"/> this is
        /// counted and shown to everybody. It writes no commit and raises no audit event: what a
        /// reader thought of a post is not a revision of it.
        /// </remarks>
        /// <param name="ownerId">The id of the liking identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="liked">The new liked state.</param>
        /// <returns>The persisted visit row, or <see langword="null"/>.</returns>
        ObjectVisit SetLike(System.Guid ownerId, System.Guid objectId, bool liked);

        /// <summary>
        /// Returns whether the supplied identity has opened the supplied object before.
        /// </summary>
        /// <remarks>
        /// It is the same row a visit is recorded in, so "read" means exactly "opened at least
        /// once" - a feed can say which entries are new to this reader without a second store.
        /// </remarks>
        /// <param name="ownerId">The id of the reading identity.</param>
        /// <param name="objectId">The id of the object.</param>
        /// <returns><see langword="true"/> when the object has been opened before.</returns>
        bool IsRead(System.Guid ownerId, System.Guid objectId);

        /// <summary>
        /// Adds a object to the manager.
        /// </summary>
        /// <param name="objectEntity">The object to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IObjectManager Add(Object objectEntity);

        /// <summary>
        /// Update a object to the manager.
        /// </summary>
        /// <param name="objectEntity">The object to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IObjectManager Update(Object objectEntity);

        /// <summary>
        /// Removes the specified object from the manager.
        /// </summary>
        /// <remarks>This method removes the specified object from the manager. If the object does
        /// not exist in the manager, no action is taken. An object that still has children is
        /// refused before anything is written - the children would otherwise keep a parent that
        /// no longer exists, and the history would record a deletion the database refused.</remarks>
        /// <param name="objectId">The object id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        /// <exception cref="HierarchyConflictException">
        /// Thrown when the object still has children; <see cref="HierarchyConflictException.Count"/>
        /// says how many.
        /// </exception>
        IObjectManager Remove(System.Guid objectId);

        /// <summary>
        /// Returns the parent of the specified object, or <c>null</c> when none is set.
        /// </summary>
        /// <param name="objectId">The id of the child object.</param>
        Object GetParent(System.Guid objectId);

        /// <summary>
        /// Returns the immediate children of the specified object.
        /// </summary>
        /// <param name="objectId">The id of the parent object.</param>
        IEnumerable<Object> GetChildren(System.Guid objectId);

        /// <summary>
        /// Returns the siblings of the specified object: every other object inside the
        /// same workspace and class. The supplied object itself is excluded from the
        /// result.
        /// </summary>
        /// <param name="objectId">The id of the reference object.</param>
        IEnumerable<Object> GetSiblings(System.Guid objectId);

        /// <summary>
        /// Returns the ancestor chain of the specified object, nearest first (parent,
        /// grandparent, …, root). The chain stops defensively when a cycle is detected
        /// in persisted data.
        /// </summary>
        /// <param name="objectId">The id of the object whose ancestors are resolved.</param>
        /// <returns>The ancestors, nearest first. The collection may be empty.</returns>
        IEnumerable<Object> GetAncestors(System.Guid objectId);

        /// <summary>
        /// Returns every descendant of the specified object (children, grandchildren, …)
        /// in breadth-first order. The traversal stops defensively when a cycle is
        /// detected in persisted data.
        /// </summary>
        /// <param name="objectId">The id of the object whose subtree is resolved.</param>
        /// <returns>The descendants in breadth-first order. The collection may be empty.</returns>
        IEnumerable<Object> GetDescendants(System.Guid objectId);

        /// <summary>
        /// Sets or clears the parent of the specified object after validating the
        /// hierarchy rules: the parent must exist, must not be the object itself, must
        /// not be one of the object's descendants (no cycles), must live in the same
        /// workspace, and — when the parent's class declares
        /// <see cref="Class.AllowedChildren"/> — the object's class must be allowed.
        /// </summary>
        /// <param name="objectId">The id of the object whose parent is set.</param>
        /// <param name="parentId">The id of the new parent, or <c>null</c> to detach.</param>
        /// <returns>
        /// The updated object, or <c>null</c> when no object with the supplied id exists.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when one of the hierarchy rules is violated.
        /// </exception>
        /// <exception cref="HierarchyConflictException">
        /// Thrown when another move of the same workspace held the hierarchy lock for too long.
        /// </exception>
        Object SetParent(System.Guid objectId, System.Guid? parentId);

        /// <summary>
        /// Sets or clears the parent of the specified object like
        /// <see cref="SetParent(System.Guid, System.Guid?)"/>, but only while the object still
        /// has the parent the caller saw.
        /// </summary>
        /// <remarks>
        /// A client that moves what it displays - a tree dragged in a browser - acts on a state
        /// it loaded earlier. When somebody else moved the object in between, carrying out the
        /// move would take it from a place the user never saw it in; the expectation turns that
        /// into a conflict the client can answer by reloading.
        /// </remarks>
        /// <param name="objectId">The id of the object whose parent is set.</param>
        /// <param name="parentId">The id of the new parent, or <c>null</c> to detach.</param>
        /// <param name="expectedParentId">
        /// The parent the caller believes the object has, or <c>null</c> when it believes the
        /// object sits at the root.
        /// </param>
        /// <returns>
        /// The updated object, or <c>null</c> when no object with the supplied id exists.
        /// </returns>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when one of the hierarchy rules is violated.
        /// </exception>
        /// <exception cref="HierarchyConflictException">
        /// Thrown when the object no longer has the expected parent, or when another move of the
        /// same workspace held the hierarchy lock for too long.
        /// </exception>
        Object SetParent(System.Guid objectId, System.Guid? parentId, System.Guid? expectedParentId);
    }

    /// <summary>
    /// Describes a completed move of an object within its workspace's hierarchy.
    /// </summary>
    /// <param name="Object">The object as it is after the move.</param>
    /// <param name="PreviousParentId">The parent it had before, or <c>null</c> for the root.</param>
    /// <param name="ParentId">The parent it has now, or <c>null</c> for the root.</param>
    public sealed record ObjectMovedEventArgs(Object Object, System.Guid? PreviousParentId, System.Guid? ParentId);

    /// <summary>
    /// A change of the object hierarchy that conflicts with the state it would be applied to -
    /// the object was moved meanwhile, still has children, or another move of the workspace
    /// is in progress. Unlike a rule violation it is not the request that is wrong but the
    /// moment, so endpoints answer it with <c>409</c>.
    /// </summary>
    /// <param name="message">The reason, in English, for logs and API clients.</param>
    /// <param name="count">The number of objects the conflict concerns (e.g. children), or zero.</param>
    public sealed class HierarchyConflictException(string message, int count = 0) : System.InvalidOperationException(message)
    {
        /// <summary>
        /// Gets the number of objects the conflict concerns (e.g. the children that keep an
        /// object from being removed), or zero.
        /// </summary>
        public int Count { get; } = count;
    }
}
