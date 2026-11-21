using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.NavigatorLinks
{
    /// <summary>
    /// Represents a selectable navigator link state for use in REST API selection scenarios.
    /// </summary>
    [Title("Navigator link state")]
    public sealed class State : RestApiSelection<NavigatorLink>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public State()
        {
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
        /// An enumerable collection of selection items that satisfy the query
        /// criteria. The collection is empty if no items match.
        /// </returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<NavigatorLink> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>()
            {
                new()
                {
                    Id = NavigatorLinkState.Active.Id(),
                    Text = I18N.Translate(request, NavigatorLinkState.Active.Text()),
                    Color = NavigatorLinkState.Active.Color()
                },
                new()
                {
                    Id = NavigatorLinkState.Hidden.Id(),
                    Text = I18N.Translate(request, NavigatorLinkState.Hidden.Text()),
                    Color = NavigatorLinkState.Hidden.Color()
                }
            };

            return list.AsQueryable();
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
        protected override IQuery<NavigatorLink> Filter(string filter, IQuery<NavigatorLink> query, IRequest request)
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
    }
}
