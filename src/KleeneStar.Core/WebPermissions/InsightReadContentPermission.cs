using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting read access to insight content,
    /// such as entities, attributes, and widgets.
    /// </summary>
    [Name("insight_read_content")]
    [Policy<InsightViewPolicy>()]
    [Policy<InsightEditPolicy>()]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightReadContentPermission : IIdentityPermission
    {
    }

}
