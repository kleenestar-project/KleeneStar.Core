using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Profile
{
    /// <summary>
    /// Serves the colleagues that can be named as a deputy to the selection on the "tenant and
    /// role" page.
    /// </summary>
    /// <remarks>
    /// A deputy takes over the tickets of an absent account, so the list holds the active
    /// members of the caller's own tenant — and never the caller, who cannot stand in for
    /// themselves.
    /// </remarks>
    [Title("Deputy")]
    public sealed class Deputy : RestApiSelection<Model.Entities.Identity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Deputy()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance.</returns>
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
        /// Retrieves the identities that can stand in for the caller.
        /// </summary>
        /// <param name="query">The query criteria, carrying the search term and the paging.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selectable deputies.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems
        (
            IQuery<Model.Entities.Identity> query,
            IQueryContext context,
            IRequest request
        )
        {
            var caller = CoreHub.IdentityManager.GetCurrentIdentity(request);

            return CoreHub.IdentityManager
                .GetIdentities(query, context)
                .Where(x => x.State == IdentityState.Active)
                .Where(x => caller is null || x.Id != caller.Id)
                // an account without a tenant belongs to the operator side and is offered to
                // everyone; a tenant member only sees the colleagues of their own tenant
                .Where(x => caller?.TenantId is null || x.TenantId == caller.TenantId)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = string.IsNullOrWhiteSpace(x.Position)
                        ? x.Name
                        : $"{x.Name} · {x.Position}"
                })
                .AsQueryable();
        }

        /// <summary>
        /// Applies the search term typed into the selection to the identity name.
        /// </summary>
        /// <param name="filter">The search term, or null when nothing was typed.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Identity> Filter
        (
            string filter,
            IQuery<Model.Entities.Identity> query,
            IRequest request
        )
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
