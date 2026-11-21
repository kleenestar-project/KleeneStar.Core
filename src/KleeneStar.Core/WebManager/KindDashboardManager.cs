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
    /// Manages the persisted object-kind dashboard layout (columns and widgets) of a
    /// workspace/kind pair. Like <see cref="KanbanBoardManager"/>, boards are never created
    /// through a user-facing form: they come into existence lazily, the first time the board is
    /// customized through <see cref="SetColumns"/> or <see cref="SetBoard"/>.
    /// </summary>
    public sealed class KindDashboardManager : IKindDashboardManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a board's layout is changed.
        /// </summary>
        public event EventHandler<KindDashboard> BoardUpdated;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private KindDashboardManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the persisted board of a workspace/kind pair, including its columns and
        /// widgets, or <see langword="null"/> when the board has never been customized.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The board, or <see langword="null"/> when none is persisted.</returns>
        public KindDashboard GetBoard(Guid workspaceId, string kind, Guid viewId = default)
        {
            return ModelHub.GetKindDashboard(workspaceId, kind, viewId);
        }

        /// <summary>
        /// Returns the persisted board of a workspace/kind pair, creating an empty one when
        /// none exists yet.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The existing or newly created board.</returns>
        public KindDashboard EnsureBoard(Guid workspaceId, string kind, Guid viewId = default)
        {
            return ModelHub.EnsureKindDashboard(workspaceId, kind, viewId);
        }

        /// <summary>
        /// Applies a column-only layout change (add / rename / resize / recolor / reorder /
        /// delete) to a board while preserving the widgets of the surviving columns.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="columns">The desired columns in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IKindDashboardManager SetColumns(Guid boardId, IReadOnlyList<KindDashboardColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);

            ModelHub.SetKindDashboardColumns(boardId, columns);

            RaiseBoardUpdated(boardId);

            return this;
        }

        /// <summary>
        /// Applies a full board update (a widget being added, deleted, reconfigured or moved) to
        /// a board, rebuilding the widgets of every column from the desired state.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="columns">
        /// The desired columns, each carrying the widgets it should hold, in their target order.
        /// Must not be null.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IKindDashboardManager SetBoard(Guid boardId, IReadOnlyList<KindDashboardColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);

            ModelHub.SetKindDashboardBoard(boardId, columns);

            RaiseBoardUpdated(boardId);

            return this;
        }

        /// <summary>
        /// Re-reads the board by id and raises <see cref="BoardUpdated"/> when it still exists.
        /// </summary>
        /// <param name="boardId">The id of the board that was changed.</param>
        private void RaiseBoardUpdated(Guid boardId)
        {
            var board = ModelHub.GetKindDashboardById(boardId);

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
