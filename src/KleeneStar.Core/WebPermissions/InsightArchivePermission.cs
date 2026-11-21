using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission enabling the archiving of an active insight.
    /// </summary>
    [Name("insight_archive")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightArchivePermission : IIdentityPermission
    {
    }

}
