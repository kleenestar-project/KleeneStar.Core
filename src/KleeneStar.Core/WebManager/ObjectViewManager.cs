using KleeneStar.Model;
using KleeneStar.Model.Entities;
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
    /// Default implementation of <see cref="IObjectViewManager"/>. Discovered and constructed
    /// by the WebExpress component manager via reflection.
    /// </summary>
    public sealed class ObjectViewManager : IObjectViewManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Fires when an object view is added.
        /// </summary>
        public event EventHandler<ObjectView> ObjectViewAdded;

        /// <summary>
        /// Fires when an object view is updated.
        /// </summary>
        public event EventHandler<ObjectView> ObjectViewUpdated;

        /// <summary>
        /// Fires when an object view is removed.
        /// </summary>
        public event EventHandler<ObjectView> ObjectViewRemoved;

        /// <summary>
        /// Initializes a new instance via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ObjectViewManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the object view with the specified id, or <c>null</c> if not found.
        /// </summary>
        /// <param name="viewId">The unique id of the view.</param>
        /// <returns>The matching object view, or <c>null</c>.</returns>
        public ObjectView GetObjectView(Guid viewId)
        {
            var query = new Query<ObjectView>()
                .Where(x => x.Id == viewId)
                .WithPaging(0, 1);

            return ModelHub.GetObjectViews(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns all object views matching the supplied query.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <returns>An enumerable collection of object views matching the query.</returns>
        public IEnumerable<ObjectView> GetObjectViews(IQuery<ObjectView> query)
        {
            return ModelHub.GetObjectViews(query);
        }

        /// <summary>
        /// Returns all object views matching the supplied query in the given context.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <param name="context">The query context.</param>
        /// <returns>An enumerable collection of object views matching the query, or an empty
        /// collection when <paramref name="context"/> is not a <see cref="KleeneStarDbContext"/>.</returns>
        public IEnumerable<ObjectView> GetObjectViews(IQuery<ObjectView> query, IQueryContext context)
        {
            if (context is KleeneStarDbContext db)
            {
                return ModelHub.GetObjectViews(query, db);
            }

            return [];
        }

        /// <summary>
        /// Returns the active object views attached to the workspace identified by <paramref name="workspaceId"/>,
        /// ordered by <see cref="ObjectView.Order"/>.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <returns>An enumerable collection of views attached to the workspace, ordered by display position.</returns>
        public IEnumerable<ObjectView> GetViewsForWorkspace(Guid workspaceId)
        {
            var query = new Query<ObjectView>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .OrderByAsc(x => x.Order);

            return ModelHub.GetObjectViews(query);
        }

        /// <summary>
        /// Returns the active object views attached to the workspace identified by
        /// <paramref name="workspaceId"/> that belong to the supplied object
        /// <paramref name="kind"/> (each kind keeps its own tab set), ordered by
        /// <see cref="ObjectView.Order"/>.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="kind">The object kind key whose tab set is requested.</param>
        /// <returns>An enumerable collection of the kind's views, ordered by display position.</returns>
        public IEnumerable<ObjectView> GetViewsForWorkspace(Guid workspaceId, string kind)
        {
            var normalized = Model.Entities.ObjectKind.Normalize(kind);

            var query = new Query<ObjectView>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEquals(x => x.Kind, normalized)
                .OrderByAsc(x => x.Order);

            return ModelHub.GetObjectViews(query);
        }

        /// <summary>
        /// Persists a new object view.
        /// </summary>
        /// <param name="viewEntry">The view to add. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IObjectViewManager AddObjectView(ObjectView viewEntry)
        {
            ModelHub.Add(viewEntry);
            ObjectViewAdded?.Invoke(this, viewEntry);
            return this;
        }

        /// <summary>
        /// Updates an existing object view.
        /// </summary>
        /// <param name="viewEntry">The view holding updated values. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IObjectViewManager UpdateObjectView(ObjectView viewEntry)
        {
            ModelHub.Update(viewEntry);
            ObjectViewUpdated?.Invoke(this, viewEntry);
            return this;
        }

        /// <summary>
        /// Removes the specified object view.
        /// </summary>
        /// <param name="viewEntry">The view to remove. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IObjectViewManager RemoveObjectView(ObjectView viewEntry)
        {
            ModelHub.Remove(viewEntry);
            ObjectViewRemoved?.Invoke(this, viewEntry);
            return this;
        }

        /// <summary>
        /// Puts the tabs of one overview of a workspace into the given order.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="order">The tab ids in their new order. Cannot be null.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        public bool ReorderViews(Guid workspaceId, string kind, IReadOnlyList<Guid> order)
        {
            ArgumentNullException.ThrowIfNull(order);

            return ModelHub.SetObjectViewOrder(workspaceId, kind, order);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing,
        /// or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // nothing to dispose
        }
    }
}
