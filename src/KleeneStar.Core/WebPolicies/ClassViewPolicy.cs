using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting read-only access to class metadata and field definitions.
    /// </summary>
    [Name("class_view_policy")]
    [Permission<ClassReadPermission>()]
    [Permission<SecurityLevelReadPermission>()]
    public sealed class ClassViewPolicy : IIdentityPolicy
    {
    }
}
