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
    /// Default implementation of <see cref="ISprintManager"/>. Discovered and constructed
    /// by the WebExpress component manager via reflection.
    /// </summary>
    public sealed class SprintManager : ISprintManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Fires when a sprint is added.
        /// </summary>
        public event EventHandler<Sprint> SprintAdded;

        /// <summary>
        /// Fires when a sprint is updated.
        /// </summary>
        public event EventHandler<Sprint> SprintUpdated;

        /// <summary>
        /// Fires when a sprint is removed.
        /// </summary>
        public event EventHandler<Sprint> SprintRemoved;

        /// <summary>
        /// Initializes a new instance via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SprintManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the sprint with the specified id, or <c>null</c> if not found.
        /// </summary>
        /// <param name="sprintId">The unique id of the sprint.</param>
        /// <returns>The matching sprint, or <c>null</c>.</returns>
        public Sprint GetSprint(Guid sprintId)
        {
            var query = new Query<Sprint>()
                .Where(x => x.Id == sprintId)
                .WithPaging(0, 1);

            return ModelHub.GetSprints(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns all sprints matching the supplied query.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <returns>An enumerable collection of sprints matching the query.</returns>
        public IEnumerable<Sprint> GetSprints(IQuery<Sprint> query)
        {
            return ModelHub.GetSprints(query);
        }

        /// <summary>
        /// Returns all sprints matching the supplied query in the given context.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <param name="context">The query context.</param>
        /// <returns>An enumerable collection of sprints matching the query, or an empty
        /// collection when <paramref name="context"/> is not a <see cref="KleeneStarDbContext"/>.</returns>
        public IEnumerable<Sprint> GetSprints(IQuery<Sprint> query, IQueryContext context)
        {
            if (context is KleeneStarDbContext db)
            {
                return ModelHub.GetSprints(query, db);
            }

            return [];
        }

        /// <summary>
        /// Returns the sprints of the workspace identified by <paramref name="workspaceId"/>,
        /// ordered by name.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <returns>An enumerable collection of sprints attached to the workspace.</returns>
        public IEnumerable<Sprint> GetSprintsForWorkspace(Guid workspaceId)
        {
            var query = new Query<Sprint>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .OrderByAsc(x => x.Name);

            return ModelHub.GetSprints(query);
        }

        /// <summary>
        /// Returns the currently active sprint of the workspace, or <c>null</c> when no
        /// sprint is running.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <returns>The active sprint, or <c>null</c>.</returns>
        public Sprint GetActiveSprint(Guid workspaceId)
        {
            return GetSprintsForWorkspace(workspaceId)
                .FirstOrDefault(x => x.State == SprintState.Active);
        }

        /// <summary>
        /// Persists a new sprint. When the new sprint is active, every other active
        /// sprint of the workspace is completed first.
        /// </summary>
        /// <param name="sprintEntry">The sprint to add. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ISprintManager AddSprint(Sprint sprintEntry)
        {
            ArgumentNullException.ThrowIfNull(sprintEntry);

            if (sprintEntry.State == SprintState.Active)
            {
                CompleteOtherActiveSprints(sprintEntry.WorkspaceId, sprintEntry.Id);
            }

            ModelHub.Add(sprintEntry);
            SprintAdded?.Invoke(this, sprintEntry);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.sprint.created", sprintEntry);

            return this;
        }

        /// <summary>
        /// Updates an existing sprint. When the update activates the sprint, every other
        /// active sprint of the same workspace is completed first.
        /// </summary>
        /// <param name="sprintEntry">The sprint holding updated values. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ISprintManager UpdateSprint(Sprint sprintEntry)
        {
            ArgumentNullException.ThrowIfNull(sprintEntry);

            if (sprintEntry.State == SprintState.Active)
            {
                CompleteOtherActiveSprints(sprintEntry.WorkspaceId, sprintEntry.Id);
            }

            ModelHub.Update(sprintEntry);
            SprintUpdated?.Invoke(this, sprintEntry);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.sprint.updated", sprintEntry);

            return this;
        }

        /// <summary>
        /// Removes the specified sprint and moves the objects committed to it back to
        /// the product backlog of their workspace.
        /// </summary>
        /// <param name="sprintEntry">The sprint to remove. Cannot be null.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ISprintManager RemoveSprint(Sprint sprintEntry)
        {
            ArgumentNullException.ThrowIfNull(sprintEntry);

            ModelHub.Remove(sprintEntry);
            SprintRemoved?.Invoke(this, sprintEntry);

            CoreHub.AddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.sprint.deleted", sprintEntry);

            return this;
        }

        /// <summary>
        /// Returns the objects of the given workspace committed to the given sprint —
        /// or, when <paramref name="sprintId"/> is <c>null</c>, the product backlog of
        /// the workspace — ordered by rank.
        /// </summary>
        /// <remarks>
        /// The members are narrowed to what the caller may read, like every object read: the
        /// sprint team's totals and the backlog are computed from them, and a classified record
        /// or one of a workspace the caller may not read must not show up even as points.
        /// </remarks>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="sprintId">The sprint id, or <c>null</c> for the backlog.</param>
        /// <returns>The matching objects, ordered by rank.</returns>
        public IReadOnlyList<Model.Entities.Object> GetSprintObjects(Guid workspaceId, Guid? sprintId)
        {
            var readable = CoreHub.ObjectManager
                .GetObjects(new Query<Model.Entities.Object>().Where(x => x.WorkspaceId == workspaceId && x.SprintId == sprintId))
                .Select(x => x.Id)
                .ToHashSet();

            return [.. ModelHub.GetObjectsBySprint(workspaceId, sprintId).Where(x => readable.Contains(x.Id))];
        }

        /// <summary>
        /// Moves the object into the given sprint (or back to the backlog when
        /// <paramref name="sprintId"/> is <c>null</c>) and re-ranks the source and
        /// target groups so the ranks stay dense and 1-based.
        /// </summary>
        /// <param name="objectId">The object to move.</param>
        /// <param name="sprintId">The target sprint, or <c>null</c> for the backlog.</param>
        /// <param name="rank">The requested 1-based rank in the target group, or
        /// <c>null</c> to append at the end.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ISprintManager MoveObjectToSprint(Guid objectId, Guid? sprintId, int? rank = null)
        {
            ModelHub.SetObjectSprint(objectId, sprintId, rank);

            return this;
        }

        /// <summary>
        /// Sets the story-point estimate of the object.
        /// </summary>
        /// <param name="objectId">The object to update.</param>
        /// <param name="points">The estimate, or <c>null</c> to clear it.</param>
        /// <returns>The current instance for method chaining.</returns>
        public ISprintManager SetStoryPoints(Guid objectId, int? points)
        {
            ModelHub.SetObjectStoryPoints(objectId, points);

            return this;
        }

        /// <summary>
        /// Completes every active sprint of the workspace except the one identified by
        /// <paramref name="exceptSprintId"/>, enforcing the single-active-sprint rule.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="exceptSprintId">The sprint that stays (or becomes) active.</param>
        private void CompleteOtherActiveSprints(Guid workspaceId, Guid exceptSprintId)
        {
            foreach (var other in GetSprintsForWorkspace(workspaceId)
                .Where(x => x.State == SprintState.Active && x.Id != exceptSprintId))
            {
                other.State = SprintState.Completed;
                ModelHub.Update(other);
                SprintUpdated?.Invoke(this, other);
            }
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
