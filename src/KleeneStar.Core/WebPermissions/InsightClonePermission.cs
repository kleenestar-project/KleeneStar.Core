using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission authorizing the duplication of an existing insight.
    /// </summary>
    [Name("insight_clone")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightClonePermission : IIdentityPermission
    {
    }

}
