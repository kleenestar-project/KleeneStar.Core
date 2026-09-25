using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// List endpoint of the global search page: the objects of every workspace matching the
    /// search, as the master side of a master-detail view whose pane shows the selected object.
    /// </summary>
    /// <remarks>
    /// The list and the results table beside it answer the same question the same way - the
    /// substring search on the summary, the WQL of the search field and the quickfilters of the
    /// saved search that runs - so switching between them keeps the result. What a caller may
    /// see is decided by <c>ObjectManager</c>, which narrows every read by permission and
    /// security level.
    /// </remarks>
    [Title("kleenestar.core:search.results.table.header")]
    [Cache]
    public sealed class List : RestApiList<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public List()
        {
        }

        /// <summary>
        /// Creates a new query context backed by the application database.
        /// </summary>
        /// <returns>An <see cref="IQueryContext"/> instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the list items: each object with its key and summary, selecting it into the
        /// detail pane of the master-detail view.
        /// </summary>
        /// <param name="query">The query (carries the applied search, filters and paging).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The list items.</returns>
        protected override IEnumerable<RestApiListItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(query, context)
                .Select(x => new RestApiListItem()
                {
                    Id = x.Id.ToString(),
                    // the results span every workspace, so the key says where one comes from
                    Text = string.IsNullOrWhiteSpace(x.Key) ? x.Summary : $"{x.Key} · {x.Summary}",
                    Image = ObjectIcon.Uri(x),
                    // the selection is handed to the master-detail composite, which fetches the
                    // reduced reading view of the object into its pane
                    PrimaryAction = new ActionMasterDetail(ListDetailControl.ControlId)
                    {
                        Uri = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog.ResolvePreviewUri(x),
                        Item = x.Id.ToString()
                    }.ToJson()
                });
        }

        /// <summary>
        /// Returns how many objects the search found in total, before paging narrows it.
        /// </summary>
        /// <param name="query">The filtered query, without paging applied.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The number of objects in the whole result.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.CountObjects(query);
        }

        /// <summary>
        /// Applies the substring search - on the summary, as the results table does.
        /// </summary>
        /// <param name="filter">The search term.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Summary, filter);
        }

        /// <summary>
        /// Applies the active quickfilters: the ones defined for the saved search that runs.
        /// </summary>
        /// <param name="filters">The active quickfilter ids.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Object> query, IRequest request)
        {
            return CustomQuickfilterSupport.Apply(filters, query, global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter.ViewKey);
        }
    }
}
