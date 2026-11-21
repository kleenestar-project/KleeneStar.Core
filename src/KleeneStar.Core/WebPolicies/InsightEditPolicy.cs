using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy authorizing management of insight content,
    /// including reading and writing content.
    /// </summary>
    [Name("insight_edit_policy")]
    [Permission<InsightReadPermission>()]
    [Permission<InsightReadContentPermission>()]
    [Permission<InsightWriteContentPermission>()]
    public sealed class InsightEditPolicy : IIdentityPolicy
    {
    }
}
