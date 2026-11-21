using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the persisted Kanban board layout (columns,
    /// swimlanes and board filter) of a workspace/kind pair.
    /// </summary>
    public interface IKanbanBoardManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a board's layout is changed.
        /// </summary>
        event EventHandler<KanbanBoard> BoardUpdated;

        /// <summary>
        /// Returns the persisted Kanban board of a workspace/kind pair, including its columns
        /// and swimlanes, or <see langword="null"/> when the board has never been customized.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The board, or <see langword="null"/> when none is persisted.</returns>
        KanbanBoard GetBoard(Guid workspaceId, string kind, Guid viewId = default);

        /// <summary>
        /// Returns the persisted Kanban board of a workspace/kind pair, creating an empty one
        /// when none exists yet.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The existing or newly created board.</returns>
        KanbanBoard EnsureBoard(Guid workspaceId, string kind, Guid viewId = default);

        /// <summary>
        /// Applies a column layout change (add / rename / recolor / reorder / delete) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="columns">The desired columns in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IKanbanBoardManager SetColumns(Guid boardId, IReadOnlyList<KanbanBoardColumn> columns);

        /// <summary>
        /// Applies a swimlane layout change (add / rename / recolor / reorder / delete) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="swimlanes">The desired swimlanes in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IKanbanBoardManager SetSwimlanes(Guid boardId, IReadOnlyList<KanbanBoardSwimlane> swimlanes);

        /// <summary>
        /// Applies the board-level WQL filter (submitted through the board settings dialog) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="filter">The WQL filter to persist, or null to clear it.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IKanbanBoardManager SetFilter(Guid boardId, string filter);
    }
}
