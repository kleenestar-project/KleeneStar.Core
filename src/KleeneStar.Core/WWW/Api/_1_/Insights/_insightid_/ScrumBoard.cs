using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The sprint board of an insight's Scrum tab: the objects the insight selects that are
    /// committed to a running sprint, on the insight's board.
    /// </summary>
    /// <remarks>
    /// Sprints belong to workspaces and an insight may span several, so "the running sprint" is
    /// the running sprint of each workspace an object of the insight lives in. The board is the
    /// selected Scrum tab's own Kanban configuration.
    /// </remarks>
    [Title("kleenestar.core:object.view.scrum.sprint.title")]
    [Cache]
    public sealed class ScrumBoard : RestApiObjectKindKanban
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
            return InsightBoardScope.Resolve(request, Model.Entities.InsightViewTypes.Scrum);
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
        /// Keeps the objects committed to a running sprint of their workspace, then applies the
        /// search term and the quickfilter chips of the view header.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered objects.</returns>
        protected override IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            var list = objects.ToList();

            var running = list
                .Select(x => x.WorkspaceId)
                .Distinct()
                .Select(CoreHub.SprintManager.GetActiveSprint)
                .Where(x => x is not null)
                .Select(x => x.Id)
                .ToHashSet();

            var inSprint = list.Where(x => x.SprintId is { } sprintId && running.Contains(sprintId));

            return ObjectKindBoardFilter.Apply(inSprint, request, InsightScope.QuickfilterView);
        }
    }
}
