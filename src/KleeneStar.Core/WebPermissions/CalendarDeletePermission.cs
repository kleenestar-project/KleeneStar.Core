using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting the right to remove a calendar.
    /// </summary>
    [Name("calendar_delete")]
    [Policy<CalendarAdminPolicy>()]
    public sealed class CalendarDeletePermission : IIdentityPermission
    {
    }
}
