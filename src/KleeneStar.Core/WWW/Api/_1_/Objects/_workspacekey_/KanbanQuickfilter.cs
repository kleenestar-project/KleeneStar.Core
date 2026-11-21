using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the
// quickfilter type argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Quickfilter endpoint of the issue Kanban board. Offers the personal scopes the board
    /// can honour as toggleable chips — starred issues, issues assigned to the caller and
    /// issues created by the caller — followed by the filters the user defined.
    /// </summary>
    /// <remarks>
    /// The view key is the issue list's, so a filter defined here shows on the list too and
    /// the other way round: they are filters over the workspace's issues, not over one
    /// presentation of them.
    ///
    /// The archived scope the list offers is absent, because the board collects active
    /// objects only and could not honour it. <see cref="ObjectKindBoardFilter"/> translates
    /// the selected ids back into filters.
    /// </remarks>
    [Cache]
    public sealed class KanbanQuickfilter : RestApiQuickfilter<ObjectEntity>
    {
        /// <summary>
        /// The key under which the quickfilters a user defined for the workspace's issues
        /// are stored. Shared with the issue list so both views offer the same filters.
        /// </summary>
        public const string ViewKey = Issues._workspacekey_.Quickfilter.ViewKey;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public KanbanQuickfilter()
        {
        }

        /// <summary>
        /// Retrieves the board quickfilter options.
        /// </summary>
        /// <param name="context">The query context (unused — the built-in options are static).</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The personal scopes followed by the user-defined filters.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindBoardFilter.StarredId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.starred")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindBoardFilter.MineId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.mine")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindBoardFilter.CreatedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.created")
            };

            // the filters the users defined follow the ones the view ships with, so the
            // familiar chips keep their position as the personal ones come and go
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            foreach (var item in CustomQuickfilterSupport.Items(ViewKey, workspaceKey, request))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Returns the record the edit dialog of a filter loads.
        /// </summary>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="id">The id of the filter.</param>
        /// <returns>The record, or null when the filter is not one of this view's.</returns>
        protected override object RetrieveItem(IQueryContext context, IRequest request, string id)
        {
            return CustomQuickfilterSupport.Read(id, ViewKey);
        }

        /// <summary>
        /// Stores a filter the user defined in the bar's editor.
        /// </summary>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="payload">The values the client supplied.</param>
        /// <returns>The stored filter, or null when it carries no name or expression.</returns>
        protected override RestApiQuickfilterItem CreateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CustomQuickfilterSupport.Create(payload, ViewKey, workspaceKey, request);
        }

        /// <summary>
        /// Changes a filter the user defined.
        /// </summary>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="payload">The values the client supplied.</param>
        /// <returns>The changed filter, or null when the id denotes none of this view's.</returns>
        protected override RestApiQuickfilterItem UpdateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            return CustomQuickfilterSupport.Update(payload, ViewKey, request);
        }

        /// <summary>
        /// Removes a filter the user defined.
        /// </summary>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="id">The id of the filter to remove.</param>
        /// <returns>True when the filter was removed.</returns>
        protected override bool DeleteItem(IQueryContext context, IRequest request, string id)
        {
            return CustomQuickfilterSupport.Delete(id, ViewKey);
        }
    }
}
