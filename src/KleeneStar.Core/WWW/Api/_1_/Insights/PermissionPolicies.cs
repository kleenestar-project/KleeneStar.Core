using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Serves the policy the permission dialog of the insight overview can grant: the creator
    /// policy alone, the one that means something on the installation-wide insight resource.
    /// </summary>
    [Cache]
    public sealed class PermissionPolicies : RestApiPermissionPoliciesScoped
    {
        /// <summary>
        /// Gets the kind of resource whose policies are offered.
        /// </summary>
        protected override string Scope => PermissionScope.Insight;

        /// <summary>
        /// Returns the creator policy only.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The policies offered.</returns>
        protected override IEnumerable<RestApiPermissionPolicy> RetrievePolicies(IRequest request)
        {
            return [.. base.RetrievePolicies(request)
                .Where(x => string.Equals(x.Id, Permission.CreatorPolicy, StringComparison.OrdinalIgnoreCase))];
        }
    }
}
