using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Profile.Tokens
{
    /// <summary>
    /// Serves the scopes a personal access token can be granted to the multi-select on the
    /// token form.
    /// </summary>
    [Title("Token scopes")]
    public sealed class Scopes : RestApiSelection<AccessToken>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Scopes()
        {
        }

        /// <summary>
        /// Retrieves the selectable scopes, named the way they appear in an API request so the
        /// chosen entries read like the token they end up on.
        /// </summary>
        /// <param name="query">The query criteria; paging is applied by the base class.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selectable scopes.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems
        (
            IQuery<AccessToken> query,
            IQueryContext context,
            IRequest request
        )
        {
            return AccessTokenScope.All
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = x.Name
                })
                .AsQueryable();
        }

        /// <summary>
        /// Applies the search term typed into the selection.
        /// </summary>
        /// <param name="filter">The search term, or null when nothing was typed.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<AccessToken> Filter
        (
            string filter,
            IQuery<AccessToken> query,
            IRequest request
        )
        {
            // a short fixed catalogue, built in RetrieveItems rather than queried
            return query;
        }
    }
}
