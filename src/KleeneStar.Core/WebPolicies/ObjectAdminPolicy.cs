using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy providing full administrative control over all objects within a workspace,
    /// including reading, updating, commenting, attaching files, linking resources,
    /// and managing object-level profiles.
    /// </summary>
    [Name("object_admin_policy")]
    [Permission<ObjectReadPermission>()]
    [Permission<ObjectUpdatePermission>()]
    [Permission<ObjectCommentPermission>()]
    [Permission<ObjectAttachPermission>()]
    [Permission<ObjectRelationPermission>()]
    [Permission<ObjectManageProfilesPermission>()]
    [Permission<ObjectReadHistoryPermission>()]
    [Permission<ObjectRestoreStatePermission>()]
    public sealed class ObjectAdminPolicy : IIdentityPolicy
    {
    }
}
