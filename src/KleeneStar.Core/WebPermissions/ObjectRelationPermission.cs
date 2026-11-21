using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission allowing management of object relations, such as references to issues or external resources.
    /// </summary>
    [Name("object_relation")]
    [Policy<ObjectEditPolicy>()]
    [Policy<ObjectAdminPolicy>()]
    public sealed class ObjectRelationPermission : IIdentityPermission
    {
    }

}
