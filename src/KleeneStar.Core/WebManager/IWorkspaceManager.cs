using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing workspaces, including adding, retrieving, and removing workspaces, as well as
    /// handling workspace-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing workspaces and events for tracking changes 
    /// to the workspace collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public interface IWorkspaceManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an workspace is added.
        /// </summary>
        event EventHandler<Workspace> WorkspaceAdded;

        /// <summary>
        /// An event that fires when an workspace is udpated.
        /// </summary>
        event EventHandler<Workspace> WorkspaceUpdated;

        /// <summary>
        /// An event that fires when an workspace is removed.
        /// </summary>
        event EventHandler<Workspace> WorkspaceRemoved;

        /// <summary>
        /// Returns a workspace based on its id.
        /// </summary>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <returns>The workspace.</returns>
        Workspace GetWorkspace(Guid workspaceId);

        /// <summary>
        /// Retrieves the workspace associated with the specified unique key.
        /// </summary>
        /// <param name="key">
        /// The unique identifier for the workspace to retrieve. Cannot be null or empty.
        /// </param>
        /// <returns>
        /// An workspace corresponding to the specified key, or null if no matching 
        /// workspace is found.
        /// </returns>
        Workspace GetWorkspaceByKey(string key);

        /// <summary>
        /// Retrieves a collection of categories that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// The query used to filter and select categories. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of categories that satisfy the query conditions. The 
        /// collection is empty if no categories match.
        /// </returns>
        IEnumerable<Category> GetCategories(IQuery<Category> query);

        /// <summary>
        /// Retrieves a collection of workspaces that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workspaces. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workspaces that match the given predicate. If no workspaces 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Workspace> GetWorkspaces(IQuery<Workspace> query);

        /// <summary>
        /// Retrieves a collection of workspaces that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workspaces. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workspaces that match the given predicate. If no workspaces 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Workspace> GetWorkspaces(IQuery<Workspace> query, IQueryContext context);

        /// <summary>
        /// Returns the active workspaces the supplied identity has favorited, ordered by name.
        /// Backs the pinned section at the top of the workspace dropdown.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <returns>The favorited workspaces. The collection may be empty.</returns>
        IReadOnlyList<Workspace> GetFavoriteWorkspaces(Guid ownerId);

        /// <summary>
        /// Returns the active workspaces the supplied identity has most recently visited,
        /// newest first, capped at <paramref name="count"/>. Backs the "recently used"
        /// section of the workspace dropdown.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="count">The maximum number of workspaces to return.</param>
        /// <returns>The recently visited workspaces, newest first. The collection may be empty.</returns>
        IReadOnlyList<Workspace> GetRecentWorkspaces(Guid ownerId, int count);

        /// <summary>
        /// Returns whether the supplied identity has favorited the supplied workspace.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <returns><see langword="true"/> when the workspace is favorited by the identity.</returns>
        bool IsFavorite(Guid ownerId, Guid workspaceId);

        /// <summary>
        /// Sets the favorite state of the supplied workspace for the supplied identity,
        /// inserting or updating the backing bookmark. Returns <see langword="null"/> when the
        /// owner or workspace does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <param name="favorite">The new favorite state.</param>
        /// <returns>The persisted bookmark, or <see langword="null"/>.</returns>
        WorkspaceBookmark SetFavorite(Guid ownerId, Guid workspaceId, bool favorite);

        /// <summary>
        /// Records that the supplied identity has just opened the supplied workspace by
        /// advancing the bookmark's last-visited timestamp (inserting the bookmark when needed).
        /// The mutation is deliberately quiet because it fires on every workspace page load.
        /// Returns <see langword="null"/> when the owner or workspace does not exist.
        /// </summary>
        /// <param name="ownerId">The id of the owning identity.</param>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <returns>The persisted bookmark, or <see langword="null"/>.</returns>
        WorkspaceBookmark RecordVisit(Guid ownerId, Guid workspaceId);

        /// <summary>
        /// Returns the home page of the supplied workspace: its dedicated document.
        /// </summary>
        /// <remarks>
        /// The home page is a dedicated document referenced by <see cref="Workspace.HomeId"/> and
        /// created by <see cref="EnsureHome"/>. It is not part of the page tree, takes no comments
        /// and is read and edited on the document overview. The read goes through the object
        /// manager; a caller without read access gets <see langword="null"/>.
        /// </remarks>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <returns>The home document, or <see langword="null"/>.</returns>
        Model.Entities.Object GetHome(Guid workspaceId);

        /// <summary>
        /// Returns whether the supplied object is the home page of its workspace.
        /// </summary>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <param name="objectId">The id of the document.</param>
        /// <returns><see langword="true"/> when the document is the workspace's home page.</returns>
        bool IsHome(Guid workspaceId, Guid objectId);

        /// <summary>
        /// Returns whether the supplied object is the home page of its workspace.
        /// </summary>
        /// <param name="object">The object, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the object is the workspace's home page.</returns>
        bool IsHome(Model.Entities.Object @object);

        /// <summary>
        /// Makes sure the supplied workspace has its dedicated home page, creating it when it has
        /// none.
        /// </summary>
        /// <remarks>
        /// The page is written into a document class of the workspace with the effective renderer
        /// prose; without such a class the class <c>Page</c> is created. A referenced document with
        /// a parent, children or comments is not adopted; it stays a page of the tree and a new
        /// home page is created.
        /// </remarks>
        /// <param name="workspaceId">The id of the workspace.</param>
        /// <param name="identityId">The author of a page that is created. Empty when not known.</param>
        /// <param name="culture">The language a created page is written in; null for the
        /// installation's own.</param>
        /// <param name="template">The template whose home page content is written, or
        /// <see langword="null"/> for the generic page.</param>
        /// <returns>The home page, or <see langword="null"/> when the workspace does not exist.</returns>
        Model.Entities.Object EnsureHome(Guid workspaceId, Guid identityId = default, System.Globalization.CultureInfo culture = null, WebWorkspaceTemplate.IWorkspaceTemplate template = null);

        /// <summary>
        /// Adds a workspace to the workspace manager.
        /// </summary>
        /// <param name="workspace">The workspace to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkspaceManager Add(Workspace workspace);

        /// <summary>
        /// Update a workspace to the workspace manager.
        /// </summary>
        /// <param name="workspace">The workspace to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkspaceManager Update(Workspace workspace);

        /// <summary>
        /// Removes the specified workspace from the workspace manager.
        /// </summary>
        /// <remarks>This method removes the specified workspace from the manager. If the workspace does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="workspaceId">The workspace id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkspaceManager Remove(Guid workspaceId);
    }
}
