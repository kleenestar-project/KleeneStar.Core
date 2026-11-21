using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting the right to change a calendar, including its business hours and
    /// holidays.
    /// </summary>
    [Name("calendar_update")]
    [Policy<CalendarEditPolicy>()]
    [Policy<CalendarAdminPolicy>()]
    public sealed class CalendarUpdatePermission : IIdentityPermission
    {
    }
}
