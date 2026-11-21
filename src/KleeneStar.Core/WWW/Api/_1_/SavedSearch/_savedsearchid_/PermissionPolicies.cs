using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_
{
    /// <summary>
    /// Serves the policies the permission dialog of a saved search can grant.
    /// </summary>
    [Cache]
    public sealed class PermissionPolicies : RestApiPermissionPoliciesScoped
    {
        /// <summary>
        /// Gets the kind of resource whose policies are offered.
        /// </summary>
        protected override string Scope => PermissionScope.SavedSearch;
    }
}
