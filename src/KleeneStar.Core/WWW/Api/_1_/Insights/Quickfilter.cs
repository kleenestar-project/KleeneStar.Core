using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Provides a quick filter implementation for insight entities in the REST API. 
    /// This class enables filtering of insight items using predefined criteria.
    /// </summary>
    public sealed class Quickfilter : RestApiQuickfilter<Model.Entities.Insight>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves a queryable collection of index items.
        /// </summary>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// An enumerable collection of quick filter items that match the 
        /// specified context and request. The collection may be empty if 
        /// no items are found.
        /// </returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_active",
                Name = "Active"
            };
        }
    }
}
