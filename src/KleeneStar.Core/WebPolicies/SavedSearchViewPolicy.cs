using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy sharing a saved search for running: its members see it in their sidebar and may
    /// run it, but not change it.
    /// </summary>
    [Name("savedsearch_view_policy")]
    [Permission<SavedSearchReadPermission>()]
    public sealed class SavedSearchViewPolicy : IIdentityPolicy
    {
    }
}
