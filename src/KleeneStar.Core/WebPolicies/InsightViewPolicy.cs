using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting read-only access to insight metadata and content.
    /// </summary>
    [Name("insight_view_policy")]
    [Permission<InsightReadPermission>()]
    [Permission<InsightReadContentPermission>()]
    public sealed class InsightViewPolicy : IIdentityPolicy
    {
    }
}
