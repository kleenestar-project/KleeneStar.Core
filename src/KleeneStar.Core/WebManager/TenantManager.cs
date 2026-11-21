using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing tenant, including adding, retrieving, and removing, as well as
    /// handling field-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing tenant and events for tracking changes 
    /// to the field collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public sealed class TenantManager : ITenantManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an tenant is added.
        /// </summary>
        public event EventHandler<Tenant> TenantAdded;

        /// <summary>
        /// An event that fires when an tenant is udpated.
        /// </summary>
        public event EventHandler<Tenant> TenantUpdated;

        /// <summary>
        /// An event that fires when an tenant is removed.
        /// </summary>
        public event EventHandler<Tenant> TenantRemoved;

        /// <summary>
        /// Returns the collection of tenants that are reserved and cannot be used for custom tenants.
        /// </summary>
        public static IEnumerable<string> ReservedTenantNames =>
        [
            "default", "admin", "system", "assets", "api", "workspace",
            "workspaces", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private TenantManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a tenant based on its id.
        /// </summary>
        /// <param name="tenantId">The id of the tenant.</param>
        /// <returns>The tenant.</returns>
        public Tenant GetTenant(Guid tenantId)
        {
            var query = new Query<Tenant>()
                .Where(x => x.Id == tenantId)
                .WithPaging(0, 1);

            return ModelHub.GetTenants(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a tenant based on its id.
        /// </summary>
        /// <param name="tenantId">The id of the tenant.</param>
        /// <returns>The tenant.</returns>
        public Tenant GetTenant(TenantIdParameter tenantId)
        {
            var guid = Guid.TryParse(tenantId.Value, out Guid id) ? id : Guid.Empty;

            return GetTenant(guid);
        }

        /// <summary>
        /// Retrieves a collection of tenants that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned tenants. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of tenants that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Tenant> GetTenants(IQuery<Tenant> query)
        {
            return ModelHub.GetTenants(query);
        }

        /// <summary>
        /// Retrieves a collection of tenants that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned tenants. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of tenants that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Tenant> GetTenants(IQuery<Tenant> query, IQueryContext context)
        {
            return ModelHub.GetTenants(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a tenant to the manager.
        /// </summary>
        /// <param name="tenantEntity">The tenant to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ITenantManager Add(Tenant tenantEntity)
        {
            ArgumentNullException.ThrowIfNull(tenantEntity);

            ModelHub.Add(tenantEntity);

            TenantAdded?.Invoke(this, tenantEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.tenant.created", tenantEntity);

            return this;
        }

        /// <summary>
        /// Update a tenant to the manager.
        /// </summary>
        /// <param name="tenantEntity">The tenant to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ITenantManager Update(Tenant tenantEntity)
        {
            ArgumentNullException.ThrowIfNull(tenantEntity);

            ModelHub.Update(tenantEntity);

            TenantUpdated?.Invoke(this, tenantEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.tenant.updated", tenantEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified tenant from the manager.
        /// </summary>
        /// <remarks>This method removes the specified tenant from the manager. If the tenant does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="tenantId">The tenant id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ITenantManager Remove(Guid tenantId)
        {
            var tenantEntry = GetTenant(tenantId);

            if (tenantEntry is not null)
            {
                ModelHub.Remove(tenantEntry);
                TenantRemoved?.Invoke(this, tenantEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
