using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission to delete a saved search somebody else owns.
    /// </summary>
    [Name("savedsearch_delete")]
    [Policy<SavedSearchAdminPolicy>()]
    public sealed class SavedSearchDeletePermission : IIdentityPermission
    {
    }
}
