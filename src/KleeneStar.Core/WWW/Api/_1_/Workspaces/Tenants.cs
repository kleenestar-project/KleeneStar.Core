using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Serves the tenants a workspace can be assigned to.
    /// </summary>
    /// <remarks>
    /// The workspace dialogs edit the assignment as a multi-select over this endpoint. The value
    /// they submit is the id list <see cref="Workspace.TenantIds"/>, which the workspace endpoint
    /// maps onto <see cref="Workspace.Tenants"/>. Only active tenants are offered.
    /// </remarks>
    [Title("kleenestar.core:workspace.tenant.label")]
    [Cache]
    public sealed class Tenants : RestApiSelection<Tenant>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tenants()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Returns the active tenants, ordered by name.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selection items.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Tenant> query, IQueryContext context, IRequest request)
        {
            return CoreHub.TenantManager
                .GetTenants(query)
                .Where(x => x.State == TenantState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Name
                })
                .AsQueryable();
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">The filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Tenant> Filter(string filter, IQuery<Tenant> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
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
