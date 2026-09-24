using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Dashboards
{
    /// <summary>
    /// Provides a dropdown component for selecting dashboard items, supporting REST API integration, filtering, and URI
    /// generation.
    /// </summary>
    [Title("Dashboard")]
    [Cache]
    public sealed class Dropdown : RestApiDropdown<Model.Entities.Dashboard>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dropdown()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>
        /// An IQueryContext instance that can be used to execute queries.
        /// </returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// An enumerable collection of dropdown items, each representing a dashboard that matches the query. The
        /// collection is empty if no dashboards are found.
        /// </returns>
        protected override IEnumerable<RestApiDropdownItem> RetrieveItems(IQuery<Model.Entities.Dashboard> query, IQueryContext context, IRequest request)
        {
            return CoreHub.DashboardManager?.GetDashboards(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context)
                .Select(x => new RestApiDropdownItem()
                {
                    Id = x.Id,
                    Text = x.Name,
                    Image = x.Icon?.Uri?.ToString(),
                    Uri = GetUri(x, request)?.ToString()
                });
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">
        /// A string representing the filter expression to apply. The format and supported 
        /// operators depend on the implementation.
        /// </param>
        /// <param name="query">
        /// The query object to which the filter will be applied.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context for resolving
        /// the appropriate REST API URI.
        /// </param>
        /// <returns>
        /// A query representing the filtered set of items that match the criteria defined by 
        /// the filter statement.
        /// </returns>
        protected override IQuery<Model.Entities.Dashboard> Filter(string filter, IQuery<Model.Entities.Dashboard> query, IRequest request)
        {
            if (filter is null || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );
        }

        /// <summary>
        /// Gets the URI associated with the specified request and dashboard item.
        /// </summary>
        /// <param name="item">
        /// The dashboard item that provides context for generating the URI. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request for which to retrieve the URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An object representing the URI for the given request and dashboard item, or null if no URI is available.
        /// </returns>
        private static IUri GetUri(Model.Entities.Dashboard item, IRequest request)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Dashboard._dashboardid_.Index>()?
                .BindParameters(new DashboardIdParameter(item?.Id.ToString()));
        }
    }
}
