using System;
using System.Collections.Generic;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Describes the board a Kanban request addresses: whose configuration it is and what it is
    /// made of. A workspace overview's board and an insight's board differ only here.
    /// </summary>
    /// <remarks>
    /// The configuration (columns, swimlanes, filter) is stored per owner and kind
    /// (<see cref="Model.Entities.KanbanBoard"/>): a workspace's board is stored under the
    /// workspace and the object kind, an insight's under the insight and
    /// <see cref="WebInsight.InsightScope.BoardKind"/>. The kind keeps the two apart, so an
    /// owner id never addresses another owner's board.
    /// </remarks>
    public sealed class KanbanBoardScope
    {
        /// <summary>
        /// Gets the id the board configuration is stored under - a workspace or an insight.
        /// </summary>
        public Guid OwnerId { get; init; }

        /// <summary>
        /// Gets the kind the board configuration is stored under.
        /// </summary>
        public string BoardKind { get; init; }

        /// <summary>
        /// Gets the narrowing that turns a query into one over the objects of the board.
        /// </summary>
        public Func<IQuery<Model.Entities.Object>, IQuery<Model.Entities.Object>> Apply { get; init; }

        /// <summary>
        /// Gets the classes the board's swimlanes may stand for - those a new swimlane claims
        /// and those the default lanes are drawn from.
        /// </summary>
        public Func<IReadOnlyList<Model.Entities.Class>> Classes { get; init; }

        /// <summary>
        /// Gets the workflow statuses the board offers, from the classes of its objects.
        /// </summary>
        public Func<KanbanStatusCatalog> Catalog { get; init; }
    }
}
