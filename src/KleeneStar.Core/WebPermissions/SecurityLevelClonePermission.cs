using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to clone a security level of a class.
    /// </summary>
    [Name("securitylevel_clone")]
    [Policy<ClassEditPolicy>()]
    [Policy<ClassAdminPolicy>()]
    public sealed class SecurityLevelClonePermission : IIdentityPermission
    {
    }
}
