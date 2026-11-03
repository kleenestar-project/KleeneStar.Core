using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Serves the groups the permission dialog of the insight overview can allow to create
    /// insights.
    /// </summary>
    [Cache]
    public sealed class PermissionGroups : RestApiPermissionGroupsAll
    {
    }
}
