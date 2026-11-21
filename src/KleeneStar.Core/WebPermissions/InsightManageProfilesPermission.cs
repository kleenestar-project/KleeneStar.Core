using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting access to manage insight profiles,
    /// including assignment of policies to global groups.
    /// </summary>
    [Name("insight_manage_profiles")]
    [Policy<InsightAdminPolicy>()]
    public sealed class InsightManageProfilesPermission : IIdentityPermission
    {
    }

}
