using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to update a security level of a class.
    /// </summary>
    [Name("securitylevel_update")]
    [Policy<ClassEditPolicy>()]
    [Policy<ClassAdminPolicy>()]
    public sealed class SecurityLevelUpdatePermission : IIdentityPermission
    {
    }
}
