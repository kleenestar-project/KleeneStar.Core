using System.Collections.Generic;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Kanban endpoint of the issue overview's classic view: the workspace's issues as a
    /// board grouped by workflow status. The board logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban"/>; this
    /// endpoint scopes it to the issue kind and applies the view's query surface.
    /// </summary>
    [Title("kleenestar.core:object.view.kanban.title")]
    [Cache]
    public sealed class Kanban : global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban
    {
        /// <summary>
        /// Gets the object kind the board is scoped to: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Applies the search term and the quickfilter chips of the board header, including
        /// the filters the user defined for the workspace's issues.
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
