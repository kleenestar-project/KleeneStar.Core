using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission authorizing modifications to insight metadata.
    /// </summary>
    [Name("insight_update")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightUpdatePermission : IIdentityPermission
    {
    }

}
