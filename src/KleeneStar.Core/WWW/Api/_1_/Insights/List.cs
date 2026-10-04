using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Provides a REST API endpoint that returns a flat list of insights for use in the
    /// sidebar navigation on the home page and the insight view pages.
    /// </summary>
    [Title("kleenestar.core:insight.list.label")]
    [Cache]
    public sealed class List : RestApiList<Model.Entities.Insight>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public List()
        {
        }

        /// <summary>
        /// Creates a new database query context.
        /// </summary>
        /// <returns>An <see cref="IQueryContext"/> for executing insight queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the list items for all insights that match the specified query, 
        /// each with a primary navigation action pointing to its detail page.
        /// </summary>
        /// <param name="query">The query criteria used to filter insights.</param>
        /// <param name="context">The database context for the query.</param>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>
        /// An enumerable of <see cref="RestApiListItem"/> objects, one per matching insight.
        /// </returns>
        protected override IEnumerable<RestApiListItem> RetrieveItems(IQuery<Model.Entities.Insight> query, IQueryContext context, IRequest request)
        {
            return CoreHub.InsightManager.GetInsights(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context)
                .Select(x => new RestApiListItem()
                {
                    Id = x.Id.ToString(),
                    Text = x.Name,
                    Image = x.Icon?.Uri?.ToString(),
                    PrimaryAction = new ActionFrame("frame",
                        CoreHub.GetUri<global::KleeneStar.Core.WWW.Insight._insightid_.Index>()?
                            .BindParameters(new InsightIdParameter(x.Id)))
                        .ToJson()
                });
        }
    }
}
