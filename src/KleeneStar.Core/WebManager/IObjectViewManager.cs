using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing <see cref="ObjectView"/> instances — the persisted
    /// tabs that wrap the objects index of a workspace.
    /// </summary>
    public interface IObjectViewManager : IComponentManager
    {
        /// <summary>
        /// Fires when an object view is added.
        /// </summary>
        event EventHandler<ObjectView> ObjectViewAdded;

        /// <summary>
        /// Fires when an object view is updated.
        /// </summary>
        event EventHandler<ObjectView> ObjectViewUpdated;

        /// <summary>
        /// Fires when an object view is removed.
        /// </summary>
        event EventHandler<ObjectView> ObjectViewRemoved;

        /// <summary>
        /// Returns the object view with the specified id, or <c>null</c> if not found.
        /// </summary>
        /// <param name="viewId">The unique id of the view.</param>
        ObjectView GetObjectView(Guid viewId);

        /// <summary>
        /// Returns all object views matching the supplied query.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        IEnumerable<ObjectView> GetObjectViews(IQuery<ObjectView> query);

        /// <summary>
        /// Returns all object views matching the supplied query in the given context.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <param name="context">The query context.</param>
        IEnumerable<ObjectView> GetObjectViews(IQuery<ObjectView> query, IQueryContext context);

        /// <summary>
        /// Returns the active object views attached to the workspace identified by <paramref name="workspaceId"/>,
        /// ordered by <see cref="ObjectView.Order"/>.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        IEnumerable<ObjectView> GetViewsForWorkspace(Guid workspaceId);

        /// <summary>
        /// Returns the active object views attached to the workspace identified by
        /// <paramref name="workspaceId"/> that belong to the supplied object
        /// <paramref name="kind"/> (each kind keeps its own tab set), ordered by
        /// <see cref="ObjectView.Order"/>.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="kind">The object kind key whose tab set is requested.</param>
        IEnumerable<ObjectView> GetViewsForWorkspace(Guid workspaceId, string kind);

        /// <summary>
        /// Persists a new object view.
        /// </summary>
        /// <param name="viewEntry">The view to add. Cannot be null.</param>
        IObjectViewManager AddObjectView(ObjectView viewEntry);

        /// <summary>
        /// Updates an existing object view.
        /// </summary>
        /// <param name="viewEntry">The view holding updated values. Cannot be null.</param>
        IObjectViewManager UpdateObjectView(ObjectView viewEntry);

        /// <summary>
        /// Removes the specified object view.
        /// </summary>
        /// <param name="viewEntry">The view to remove. Cannot be null.</param>
        IObjectViewManager RemoveObjectView(ObjectView viewEntry);

        /// <summary>
        /// Puts the tabs of one overview of a workspace - its views of one object kind - into
        /// the given order. A tab the list does not name keeps its place behind the named ones.
        /// </summary>
        /// <remarks>
        /// Arranging tabs is live editing, like the insight tabs: no
        /// <see cref="ObjectViewUpdated"/> is raised per moved tab.
        /// </remarks>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="order">The tab ids in their new order. Cannot be null.</param>
        /// <returns><see langword="true"/> when at least one tab of the overview was named.</returns>
        bool ReorderViews(Guid workspaceId, string kind, IReadOnlyList<Guid> order);
    }
}
