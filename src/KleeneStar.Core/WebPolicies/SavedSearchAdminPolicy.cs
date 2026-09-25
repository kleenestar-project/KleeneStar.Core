using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy handing a saved search over entirely: its members may run, change and delete it
    /// and decide who else may - everything its owner may, except starring it for the owner.
    /// </summary>
    [Name("savedsearch_admin_policy")]
    [Permission<SavedSearchReadPermission>()]
    [Permission<SavedSearchUpdatePermission>()]
    [Permission<SavedSearchDeletePermission>()]
    [Permission<SavedSearchManageProfilesPermission>()]
    public sealed class SavedSearchAdminPolicy : IIdentityPolicy
    {
    }
}
