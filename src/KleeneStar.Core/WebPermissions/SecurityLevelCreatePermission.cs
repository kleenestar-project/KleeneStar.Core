using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to create a security level of a class.
    /// </summary>
    [Name("securitylevel_create")]
    [Policy<ClassEditPolicy>()]
    [Policy<ClassAdminPolicy>()]
    public sealed class SecurityLevelCreatePermission : IIdentityPermission
    {
    }
}
