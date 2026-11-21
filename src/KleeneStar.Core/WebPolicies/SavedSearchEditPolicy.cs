using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy sharing a saved search for maintenance: its members may run it and change its
    /// name, query and description.
    /// </summary>
    [Name("savedsearch_edit_policy")]
    [Permission<SavedSearchReadPermission>()]
    [Permission<SavedSearchUpdatePermission>()]
    public sealed class SavedSearchEditPolicy : IIdentityPolicy
    {
    }
}
