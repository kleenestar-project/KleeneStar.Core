using System.Collections.Generic;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Calendar endpoint of the issue overview's scheduler view: the workspace's issues as
    /// entries on a month, week or agenda grid. The calendar logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindSchedule"/>; this
    /// endpoint scopes it to the issue kind and applies the view's query surface.
    /// </summary>
    /// <remarks>
    /// Unlike the sibling <see cref="Gantt"/> endpoint this one needs no sub-paths: the
    /// schedule control persists a moved entry with a PUT against the base address, carrying
    /// the entry id in the payload.
    /// </remarks>
    [Title("kleenestar.core:object.view.scheduler.title")]
    [Cache]
    public sealed class Scheduler : global::KleeneStar.Core.WebRestApi.RestApiObjectKindSchedule
    {
        /// <summary>
        /// Gets the object kind the calendar is scoped to: issues.
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
