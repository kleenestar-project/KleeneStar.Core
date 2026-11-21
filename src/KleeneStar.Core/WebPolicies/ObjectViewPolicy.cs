using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting read-only access to objects and their content.
    /// </summary>
    [Name("object_view_policy")]
    [Permission<ObjectReadPermission>()]
    [Permission<ObjectReadHistoryPermission>()]
    public sealed class ObjectViewPolicy : IIdentityPolicy
    {
    }

}
