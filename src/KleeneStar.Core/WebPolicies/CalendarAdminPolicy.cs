using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting full administrative access to a calendar.
    /// </summary>
    [Name("calendar_admin_policy")]
    [Permission<CalendarReadPermission>()]
    [Permission<CalendarUpdatePermission>()]
    [Permission<CalendarDeletePermission>()]
    public sealed class CalendarAdminPolicy : IIdentityPolicy
    {
    }
}
