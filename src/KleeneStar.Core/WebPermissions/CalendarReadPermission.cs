using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting read access to a calendar, including its business hours and holidays.
    /// </summary>
    [Name("calendar_read")]
    [Policy<CalendarViewPolicy>()]
    [Policy<CalendarEditPolicy>()]
    [Policy<CalendarAdminPolicy>()]
    public sealed class CalendarReadPermission : IIdentityPermission
    {
    }
}
