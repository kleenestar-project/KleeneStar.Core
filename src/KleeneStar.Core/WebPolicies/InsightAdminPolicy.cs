using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy providing full administrative control over an insight,
    /// including creation, modification, deletion, lifecycle operations,
    /// content management, and profile administration.
    /// </summary>
    [Name("insight_admin_policy")]
    [Permission<InsightCreatePermission>()]
    [Permission<InsightReadPermission>()]
    [Permission<InsightUpdatePermission>()]
    [Permission<InsightDeletePermission>()]
    [Permission<InsightArchivePermission>()]
    [Permission<InsightRestorePermission>()]
    [Permission<InsightClonePermission>()]
    [Permission<InsightManageProfilesPermission>()]
    [Permission<InsightReadContentPermission>()]
    [Permission<InsightWriteContentPermission>()]
    public sealed class InsightAdminPolicy : IIdentityPolicy
    {
    }
}
