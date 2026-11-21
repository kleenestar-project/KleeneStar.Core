using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing <see cref="Sprint"/> instances — the Scrum
    /// iterations of a workspace — and the sprint assignment of the workspace objects
    /// (commitment, ranking, story-point estimation).
    /// </summary>
    public interface ISprintManager : IComponentManager
    {
        /// <summary>
        /// Fires when a sprint is added.
        /// </summary>
        event EventHandler<Sprint> SprintAdded;

        /// <summary>
        /// Fires when a sprint is updated.
        /// </summary>
        event EventHandler<Sprint> SprintUpdated;

        /// <summary>
        /// Fires when a sprint is removed.
        /// </summary>
        event EventHandler<Sprint> SprintRemoved;

        /// <summary>
        /// Returns the sprint with the specified id, or <c>null</c> if not found.
        /// </summary>
        /// <param name="sprintId">The unique id of the sprint.</param>
        Sprint GetSprint(Guid sprintId);

        /// <summary>
        /// Returns all sprints matching the supplied query.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        IEnumerable<Sprint> GetSprints(IQuery<Sprint> query);

        /// <summary>
        /// Returns all sprints matching the supplied query in the given context.
        /// </summary>
        /// <param name="query">The query criteria. Cannot be null.</param>
        /// <param name="context">The query context.</param>
        IEnumerable<Sprint> GetSprints(IQuery<Sprint> query, IQueryContext context);

        /// <summary>
        /// Returns the sprints of the workspace identified by <paramref name="workspaceId"/>,
        /// ordered by name.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        IEnumerable<Sprint> GetSprintsForWorkspace(Guid workspaceId);

        /// <summary>
        /// Returns the currently active sprint of the workspace, or <c>null</c> when no
        /// sprint is running.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        Sprint GetActiveSprint(Guid workspaceId);

        /// <summary>
        /// Persists a new sprint.
        /// </summary>
        /// <param name="sprintEntry">The sprint to add. Cannot be null.</param>
        ISprintManager AddSprint(Sprint sprintEntry);

        /// <summary>
        /// Updates an existing sprint. When the update activates the sprint, every other
        /// active sprint of the same workspace is completed first.
        /// </summary>
        /// <param name="sprintEntry">The sprint holding updated values. Cannot be null.</param>
        ISprintManager UpdateSprint(Sprint sprintEntry);

        /// <summary>
        /// Removes the specified sprint and moves the objects committed to it back to
        /// the product backlog of their workspace.
        /// </summary>
        /// <param name="sprintEntry">The sprint to remove. Cannot be null.</param>
        ISprintManager RemoveSprint(Sprint sprintEntry);

        /// <summary>
        /// Returns the objects of the given workspace committed to the given sprint —
        /// or, when <paramref name="sprintId"/> is <c>null</c>, the product backlog of
        /// the workspace — ordered by rank.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <param name="sprintId">The sprint id, or <c>null</c> for the backlog.</param>
        IReadOnlyList<Model.Entities.Object> GetSprintObjects(Guid workspaceId, Guid? sprintId);

        /// <summary>
        /// Moves the object into the given sprint (or back to the backlog when
        /// <paramref name="sprintId"/> is <c>null</c>) and re-ranks the source and
        /// target groups so the ranks stay dense and 1-based.
        /// </summary>
        /// <param name="objectId">The object to move.</param>
        /// <param name="sprintId">The target sprint, or <c>null</c> for the backlog.</param>
        /// <param name="rank">The requested 1-based rank in the target group, or
        /// <c>null</c> to append at the end.</param>
        ISprintManager MoveObjectToSprint(Guid objectId, Guid? sprintId, int? rank = null);

        /// <summary>
        /// Sets the story-point estimate of the object.
        /// </summary>
        /// <param name="objectId">The object to update.</param>
        /// <param name="points">The estimate, or <c>null</c> to clear it.</param>
        ISprintManager SetStoryPoints(Guid objectId, int? points);
    }
}
