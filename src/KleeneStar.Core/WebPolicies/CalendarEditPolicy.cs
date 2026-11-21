using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting the right to read and change a calendar.
    /// </summary>
    [Name("calendar_edit_policy")]
    [Permission<CalendarReadPermission>()]
    [Permission<CalendarUpdatePermission>()]
    public sealed class CalendarEditPolicy : IIdentityPolicy
    {
    }
}
