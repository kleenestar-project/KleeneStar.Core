using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.Dashboards
{
    /// <summary>
    /// Provides a REST API endpoint that returns a flat list of dashboards for use in the
    /// sidebar navigation on the home page and the dashboard view pages.
    /// </summary>
    [Title("kleenestar.core:dashboard.list.label")]
    [Cache]
    public sealed class List : RestApiList<Model.Entities.Dashboard>
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
        /// <returns>An <see cref="IQueryContext"/> for executing dashboard queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the list items for all dashboards that match the specified query, 
        /// each with a primary navigation action pointing to its detail page.
        /// </summary>
        /// <param name="query">The query criteria used to filter dashboards.</param>
        /// <param name="context">The database context for the query.</param>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>
        /// An enumerable of <see cref="RestApiListItem"/> objects, one per matching dashboard.
        /// </returns>
        protected override IEnumerable<RestApiListItem> RetrieveItems(IQuery<Model.Entities.Dashboard> query, IQueryContext context, IRequest request)
        {
            return CoreHub.DashboardManager.GetDashboards(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context)
                .Select(x => new RestApiListItem()
                {
                    Id = x.Id.ToString(),
                    Text = x.Name,
                    Image = x.Icon?.Uri?.ToString(),
                    PrimaryAction = new ActionFrame("frame",
                        CoreHub.GetUri<global::KleeneStar.Core.WWW.Dashboard._dashboardid_.Index>()?
                            .BindParameters(new DashboardIdParameter(x.Id)))
                        .ToJson()
                });
        }
    }
}
