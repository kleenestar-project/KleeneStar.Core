using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission allowing the creation of new, isolated insights.
    /// </summary>
    [Name("insight_create")]
    [Policy<InsightCreatorPolicy>()]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightCreatePermission : IIdentityPermission
    {
    }

}
