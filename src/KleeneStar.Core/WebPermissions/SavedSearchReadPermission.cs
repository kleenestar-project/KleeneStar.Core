using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to see a saved search somebody else owns and to run it.
    /// </summary>
    [Name("savedsearch_read")]
    [Policy<SavedSearchViewPolicy>()]
    [Policy<SavedSearchEditPolicy>()]
    [Policy<SavedSearchAdminPolicy>()]
    public sealed class SavedSearchReadPermission : IIdentityPermission
    {
    }
}
