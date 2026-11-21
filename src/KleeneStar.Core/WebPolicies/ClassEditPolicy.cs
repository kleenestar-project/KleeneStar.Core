using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy allowing creation and modification of class definitions,
    /// including cloning and updating metadata or fields.
    /// </summary>
    [Name("class_edit_policy")]
    [Permission<ClassCreatePermission>()]
    [Permission<ClassReadPermission>()]
    [Permission<ClassUpdatePermission>()]
    [Permission<ClassClonePermission>()]
    [Permission<SecurityLevelReadPermission>()]
    [Permission<SecurityLevelCreatePermission>()]
    [Permission<SecurityLevelUpdatePermission>()]
    [Permission<SecurityLevelClonePermission>()]
    public sealed class ClassEditPolicy : IIdentityPolicy
    {
    }
}
