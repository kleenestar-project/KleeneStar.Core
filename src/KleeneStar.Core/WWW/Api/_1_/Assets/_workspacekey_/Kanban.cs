using System.Collections.Generic;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Kanban endpoint of the asset overview's classic view: the workspace's assets as a
    /// board grouped by workflow status. The board logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban"/>; this
    /// endpoint scopes it to the asset kind and applies the view's query surface. It is an
    /// independent sibling of the issue Kanban endpoint (not a subclass), so both keep
    /// their own route.
    /// </summary>
    [Title("kleenestar.core:object.view.kanban.title")]
    [Cache]
    public sealed class Kanban : global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban
    {
        /// <summary>
        /// Gets the object kind the board is scoped to: assets.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;

        /// <summary>
        /// Applies the search term and the quickfilter chips of the board header, including
        /// the filters the user defined for the workspace's assets.
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
