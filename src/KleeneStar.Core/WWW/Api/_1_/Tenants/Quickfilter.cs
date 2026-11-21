using KleeneStar.Core.WebQuickfilter;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Tenants
{
    /// <summary>
    /// Provides a quick filter implementation for tenant entities in the REST API. 
    /// This class enables filtering of filed items using predefined criteria.
    /// </summary>
    public sealed class Quickfilter : RestApiQuickfilter<Model.Entities.Tenant>
    {
        /// <summary>
        /// The key under which the quickfilters a user defined for this view are stored.
        /// </summary>
        /// <remarks>
        /// The bar and the table have to agree on it, so it is named once here and read from both.
        /// </remarks>
        public const string ViewKey = "tenants";

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

            // the filters the users defined follow the ones the view ships with, so the familiar
            // chips keep their position as the personal ones come and go
            foreach (var item in CustomQuickfilterSupport.Items(ViewKey, null, request))
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
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="payload">The values the client supplied.</param>
        /// <returns>The stored filter, or null when it carries no name or expression.</returns>
        protected override RestApiQuickfilterItem CreateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            return CustomQuickfilterSupport.Create(payload, ViewKey, null, request);
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
