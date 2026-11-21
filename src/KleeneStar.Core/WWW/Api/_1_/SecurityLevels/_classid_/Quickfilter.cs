using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels._classid_
{
    /// <summary>
    /// Provides a quick filter implementation for security level entities in the REST API.
    /// </summary>
    public sealed class Quickfilter : RestApiQuickfilter<Model.Entities.SecurityLevel>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves the quick filters offered above the security level table.
        /// </summary>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The quick filter items, which may be empty.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_active",
                Name = "Active"
            };

            // the levels nobody is cleared for are the ones worth finding: a level whose group
            // list is empty hides every record it is put on
            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_closed",
                Name = "Closed"
            };
        }
    }
}
