using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting read access to the security levels defined on a class.
    /// </summary>
    /// <remarks>
    /// Reading the catalog is not the same as being cleared for a level: the clearance is the
    /// group list a level names and is evaluated by the security level manager, not here. This
    /// permission governs the administration surface - who may look at what a class classifies
    /// its objects with.
    /// </remarks>
    [Name("securitylevel_read")]
    [Policy<ClassViewPolicy>()]
    [Policy<ClassEditPolicy>()]
    [Policy<ClassAdminPolicy>()]
    public sealed class SecurityLevelReadPermission : IIdentityPermission
    {
    }
}
