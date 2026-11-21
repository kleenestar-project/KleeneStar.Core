using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_
{
    /// <summary>
    /// Serves the policies the permission dialog of a workspace can grant.
    /// </summary>
    [Cache]
    public sealed class PermissionPolicies : RestApiPermissionPoliciesScoped
    {
        /// <summary>
        /// Gets the kind of resource whose policies are offered.
        /// </summary>
        protected override string Scope => PermissionScope.Workspace;
    }
}
