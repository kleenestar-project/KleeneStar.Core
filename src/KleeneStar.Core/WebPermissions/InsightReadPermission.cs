using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting read access to insight metadata,
    /// including name, description, and status.
    /// </summary>
    [Name("insight_read")]
    [Policy<InsightViewPolicy>()]
    [Policy<InsightEditPolicy>()]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightReadPermission : IIdentityPermission
    {
    }

}
