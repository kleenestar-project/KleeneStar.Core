using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission allowing creation, modification, and deletion of insight content.
    /// </summary>
    [Name("insight_write_content")]
    [Policy<InsightEditPolicy>()]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightWriteContentPermission : IIdentityPermission
    {
    }

}
