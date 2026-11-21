using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to change the name, the query and the description of a saved search
    /// somebody else owns.
    /// </summary>
    [Name("savedsearch_update")]
    [Policy<SavedSearchEditPolicy>()]
    [Policy<SavedSearchAdminPolicy>()]
    public sealed class SavedSearchUpdatePermission : IIdentityPermission
    {
    }
}
