using System.Collections.Generic;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Gantt endpoint of the issue overview's timeline view: the workspace's issues as bars on
    /// a plan. The timeline logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindGantt"/>; this endpoint
    /// scopes it to the issue kind and applies the view's query surface.
    /// </summary>
    /// <remarks>
    /// <see cref="IncludeSubPathsAttribute"/> is REQUIRED: the control persists a moved bar
    /// against <c>{base}/tasks/{id}</c> and a dependency against <c>{base}/links</c>. Without
    /// it those sub-paths 404 and every drag silently reverts on the next load.
    /// </remarks>
    [Title("kleenestar.core:object.view.gantt.title")]
    [IncludeSubPaths(true)]
    [Cache]
    public sealed class Gantt : global::KleeneStar.Core.WebRestApi.RestApiObjectKindGantt
    {
        /// <summary>
        /// Gets the object kind the plan is scoped to: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Applies the search term and the quickfilter chips of the view header, including the
        /// filters the user defined for the workspace's issues.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request carrying the query surface and the caller.</param>
        /// <returns>The filtered objects.</returns>
        protected override IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            return global::KleeneStar.Core.WebRestApi.ObjectKindBoardFilter.Apply
            (
                objects,
                request,
                KanbanQuickfilter.ViewKey
            );
        }
    }
}
