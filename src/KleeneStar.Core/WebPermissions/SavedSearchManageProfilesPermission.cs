using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to decide who else may see, change or delete a saved search - to open its
    /// permission dialog.
    /// </summary>
    [Name("savedsearch_manage_profiles")]
    [Policy<SavedSearchAdminPolicy>()]
    public sealed class SavedSearchManageProfilesPermission : IIdentityPermission
    {
    }
}
