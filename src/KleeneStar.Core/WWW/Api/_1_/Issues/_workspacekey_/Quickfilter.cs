using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the
// quickfilter type argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_
{
    /// <summary>
    /// Quickfilter endpoint of the issue overview. Offers the personal scopes as
    /// toggleable chips: starred issues, issues assigned to the caller, issues created
    /// by the caller, and the archived history. The <see cref="Table"/> endpoint
    /// translates the selected ids back into filters.
    /// </summary>
    [Cache]
    public sealed class Quickfilter : RestApiQuickfilter<ObjectEntity>
    {
        /// <summary>
        /// The key under which the quickfilters a user defined for this view are stored.
        /// </summary>
        /// <remarks>
        /// The bar and the table have to agree on it, so it is named once here and read from both.
        /// The filters are additionally narrowed by the workspace, so one workspace's own chips do
        /// not turn up in every other one.
        /// </remarks>
        public const string ViewKey = "issues";

        /// <summary>
        /// The quickfilter id prefix shared by every issue chip.
        /// </summary>
        public const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the starred-issues chip.</summary>
        public const string StarredId = IdPrefix + "starred";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        public const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the created-by-me chip.</summary>
        public const string CreatedId = IdPrefix + "created";

        /// <summary>Quickfilter id of the archived chip.</summary>
        public const string ArchivedId = IdPrefix + "archived";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves the issue quickfilter options.
        /// </summary>
        /// <param name="context">The query context (unused — the options are static).</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The personal issue scopes as quickfilter items.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = StarredId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.starred")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = MineId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.mine")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = CreatedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.created")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ArchivedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.archived")
            };

            // the filters the users defined follow the ones the view ships with, so the familiar
            // chips keep their position as the personal ones come and go
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            foreach (var item in CustomQuickfilterSupport.Items(ViewKey, workspaceKey, request))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Returns the record the edit dialog of a filter loads.
        /// </summary>
        /// <remarks>
        /// Overridden so the record also carries whether the filter is shared, which the
        /// framework's own record has no field for.
        /// </remarks>
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
        /// <remarks>
        /// The workspace comes from the route the bar is served under, so a filter defined here
        /// belongs to this workspace's issue list rather than to every workspace's.
        /// </remarks>
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
