using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the persisted Kanban board layout (columns, swimlanes and board filter) of a
    /// workspace/kind pair. Unlike <see cref="InsightManager"/>, boards are never created
    /// through a user-facing form: they come into existence lazily, the first time the board is
    /// customized through <see cref="SetColumns"/>, <see cref="SetSwimlanes"/> or
    /// <see cref="SetFilter"/>.
    /// </summary>
    public sealed class KanbanBoardManager : IKanbanBoardManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a board's layout is changed.
        /// </summary>
        public event EventHandler<KanbanBoard> BoardUpdated;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private KanbanBoardManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the persisted Kanban board of a workspace/kind pair, including its columns
        /// and swimlanes, or <see langword="null"/> when the board has never been customized.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The board, or <see langword="null"/> when none is persisted.</returns>
        public KanbanBoard GetBoard(Guid workspaceId, string kind, Guid viewId = default)
        {
            return ModelHub.GetKanbanBoard(workspaceId, kind, viewId);
        }

        /// <summary>
        /// Returns the persisted Kanban board of a workspace/kind pair, creating an empty one
        /// when none exists yet.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The existing or newly created board.</returns>
        public KanbanBoard EnsureBoard(Guid workspaceId, string kind, Guid viewId = default)
        {
            return ModelHub.EnsureKanbanBoard(workspaceId, kind, viewId);
        }

        /// <summary>
        /// Applies a column layout change (add / rename / recolor / reorder / delete) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="columns">The desired columns in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IKanbanBoardManager SetColumns(Guid boardId, IReadOnlyList<KanbanBoardColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);

            ModelHub.SetKanbanColumns(boardId, columns);

            RaiseBoardUpdated(boardId);

            return this;
        }

        /// <summary>
        /// Applies a swimlane layout change (add / rename / recolor / reorder / delete) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="swimlanes">The desired swimlanes in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IKanbanBoardManager SetSwimlanes(Guid boardId, IReadOnlyList<KanbanBoardSwimlane> swimlanes)
        {
            ArgumentNullException.ThrowIfNull(swimlanes);

            ModelHub.SetKanbanSwimlanes(boardId, swimlanes);

            RaiseBoardUpdated(boardId);

            return this;
        }

        /// <summary>
        /// Applies the board-level WQL filter (submitted through the board settings dialog) to a
        /// Kanban board.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="filter">The WQL filter to persist, or null to clear it.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IKanbanBoardManager SetFilter(Guid boardId, string filter)
        {
            ModelHub.SetKanbanFilter(boardId, filter);

            RaiseBoardUpdated(boardId);

            return this;
        }

        /// <summary>
        /// Re-reads the board by id and raises <see cref="BoardUpdated"/> when it still exists.
        /// </summary>
        /// <param name="boardId">The id of the board that was changed.</param>
        private void RaiseBoardUpdated(Guid boardId)
        {
            var board = ModelHub.GetKanbanBoardById(boardId);

            if (board is not null)
            {
                BoardUpdated?.Invoke(this, board);
            }
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
