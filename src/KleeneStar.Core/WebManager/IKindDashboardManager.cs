using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the persisted object-kind dashboard layout (columns
    /// and widgets) of a workspace/kind pair — the KPI board shown on the Dashboard tab of the
    /// issues/assets overview.
    /// </summary>
    public interface IKindDashboardManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a board's layout is changed.
        /// </summary>
        event EventHandler<KindDashboard> BoardUpdated;

        /// <summary>
        /// Returns the persisted board of a workspace/kind pair, including its columns and
        /// widgets, or <see langword="null"/> when the board has never been customized.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The board, or <see langword="null"/> when none is persisted.</returns>
        KindDashboard GetBoard(Guid workspaceId, string kind, Guid viewId = default);

        /// <summary>
        /// Returns the persisted board of a workspace/kind pair, creating an empty one when
        /// none exists yet.
        /// </summary>
        /// <param name="workspaceId">The workspace the board belongs to.</param>
        /// <param name="kind">The object kind the board is scoped to.</param>
        /// <param name="viewId">The owning tab identifier, or the legacy board when empty.</param>
        /// <returns>The existing or newly created board.</returns>
        KindDashboard EnsureBoard(Guid workspaceId, string kind, Guid viewId = default);

        /// <summary>
        /// Applies a column-only layout change (add / rename / resize / recolor / reorder /
        /// delete) to a board while preserving the widgets of the surviving columns.
        /// </summary>
        /// <param name="boardId">The id of the board to update.</param>
        /// <param name="columns">The desired columns in their target order. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IKindDashboardManager SetColumns(Guid boardId, IReadOnlyList<KindDashboardColumn> columns);

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
        IKindDashboardManager SetBoard(Guid boardId, IReadOnlyList<KindDashboardColumn> columns);
    }
}
