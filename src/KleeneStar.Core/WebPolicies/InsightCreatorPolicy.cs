using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Global policy allowing the creation of new insights.
    /// </summary>
    [Name("insight_creator_policy")]
    [Permission<InsightCreatePermission>()]
    public sealed class InsightCreatorPolicy : IIdentityPolicy
    {
    }
}
