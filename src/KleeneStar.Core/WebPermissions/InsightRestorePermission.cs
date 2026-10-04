using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission allowing the restoration of an archived insight.
    /// </summary>
    [Name("insight_restore")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightRestorePermission : IIdentityPermission
    {
    }

}
