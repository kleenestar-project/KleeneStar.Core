using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The Kanban board of an insight: the active objects its query selects as cards, placed by
    /// their workflow status, with swimlanes per class. A drop is a workflow transition, exactly
    /// as on a workspace's board.
    /// </summary>
    /// <remarks>
    /// The board configuration belongs to the selected tab of the insight. Arranging it needs the right to change the insight's content;
    /// moving a card needs, as everywhere, the right to transition that object.
    /// </remarks>
    [Title("kleenestar.core:insight.view.kanban.label")]
    [Cache]
    public sealed class Kanban : RestApiObjectKindKanban
    {
        /// <summary>
        /// Gets the kind the base falls back to; the scope below replaces the kind narrowing.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Resolves the insight's board, or none for a caller who may not read the insight.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The board scope.</returns>
        protected override KanbanBoardScope ResolveScope(IRequest request)
        {
            return InsightBoardScope.Resolve(request);
        }

        /// <summary>
        /// Arranging the board is changing the insight.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may arrange the board.</returns>
        protected override bool MayArrange(IRequest request)
        {
            return InsightScope.MayArrange(InsightScope.Resolve(request), request);
        }

        /// <summary>
        /// Applies the search term and the quickfilter chips of the board header.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered objects.</returns>
        protected override IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            return ObjectKindBoardFilter.Apply(objects, request, InsightScope.QuickfilterView);
        }
    }

    /// <summary>
    /// Builds the board scope of an insight, shared by its Kanban board and its sprint board.
    /// </summary>
    internal static class InsightBoardScope
    {
        /// <summary>
        /// Resolves the board scope of the insight the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="viewType">The tab type that owns the board.</param>
        /// <returns>The scope, or <see langword="null"/> when there is no readable insight.</returns>
        public static KanbanBoardScope Resolve(IRequest request, string viewType = Model.Entities.InsightViewTypes.Kanban)
        {
            // the board asks for its scope once per hook (columns, cards, lanes, statuses), and
            // the answer is the same for every one of them within a request
            if (request is null)
            {
                return Build(null, viewType);
            }

            var scopes = _scopes.GetValue(request, _ => new ConcurrentDictionary<string, KanbanBoardScope>(StringComparer.OrdinalIgnoreCase));

            return scopes.GetOrAdd(viewType ?? string.Empty, _ => Build(request, viewType));
        }

        /// <summary>
        /// The scopes resolved for a request, by view type. An entry lives as long as its
        /// request.
        /// </summary>
        private static readonly ConditionalWeakTable<IRequest, ConcurrentDictionary<string, KanbanBoardScope>> _scopes = [];

        /// <summary>
        /// Builds the scope of the insight the request addresses.
        /// </summary>
        /// <param name="request">The request naming the insight.</param>
        /// <param name="viewType">The type of the view the board belongs to.</param>
        /// <returns>The scope, or <see langword="null"/> when the caller may not read the insight.</returns>
        private static KanbanBoardScope Build(IRequest request, string viewType)
        {
            var insight = InsightScope.ResolveReadable(request);

            if (insight is null)
            {
                return null;
            }

            // the classes and the statuses come from the objects the insight selects, so they
            // are read once per request and shared by the lanes and the status catalog
            IReadOnlyList<Model.Entities.Class> classes = null;
            IReadOnlyList<Model.Entities.Class> Classes() => classes ??= InsightScope.GetClasses(insight);

            return new KanbanBoardScope
            {
                OwnerId = insight.Id,
                ViewId = BoardViewScope.Insight(request, insight.Id, viewType),
                BoardKind = InsightScope.BoardKind,
                Apply = query => InsightScope.Apply(insight, query),
                Classes = Classes,
                Catalog = () => KanbanStatusCatalog.Build(Classes())
            };
        }
    }
}
