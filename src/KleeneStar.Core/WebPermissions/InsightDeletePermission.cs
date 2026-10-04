using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission allowing the permanent deletion of an insight.
    /// </summary>
    [Name("insight_delete")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightDeletePermission : IIdentityPermission
    {
    }

}
