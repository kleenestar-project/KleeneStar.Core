using KleeneStar.Core.WebPermission;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Serves the policies a permission dialog can grant on one kind of resource.
    /// </summary>
    /// <remarks>
    /// The list is the running application's own registry of policies narrowed to the resource, so
    /// a dialog offers exactly what its guards can check and a policy added in code shows up
    /// without anything being seeded or maintained alongside it.
    /// </remarks>
    public abstract class RestApiPermissionPoliciesScoped : RestApiPermissionPolicies
    {
        /// <summary>
        /// Gets the kind of resource whose policies are offered, as named in
        /// <see cref="PermissionScope"/>.
        /// </summary>
        protected abstract string Scope { get; }

        /// <summary>
        /// Returns the policies that can be granted on this kind of resource.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The policies, labelled by the role they name.</returns>
        protected override IEnumerable<RestApiPermissionPolicy> RetrievePolicies(IRequest request)
        {
            return [.. PolicyCatalog.GetPolicies(Scope)
                .Select(x => new RestApiPermissionPolicy()
                {
                    Id = x,
                    Name = PolicyCatalog.GetLabel(x, Scope),
                    // the registered name is what a guard checks, so it is shown alongside the
                    // label rather than hidden: it is the term to search for in the code
                    Description = x
                })];
        }
    }
}
