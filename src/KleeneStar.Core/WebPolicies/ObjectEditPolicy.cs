using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy authorizing active interaction with objects,
    /// including reading, updating, commenting, attaching files, and managing links.
    /// </summary>
    [Name("object_edit_policy")]
    [Permission<ObjectReadPermission>()]
    [Permission<ObjectUpdatePermission>()]
    [Permission<ObjectCommentPermission>()]
    [Permission<ObjectAttachPermission>()]
    [Permission<ObjectRelationPermission>()]
    [Permission<ObjectReadHistoryPermission>()]
    [Permission<ObjectRestoreStatePermission>()]
    public sealed class ObjectEditPolicy : IIdentityPolicy
    {
    }
}
