using KleeneStar.Core.WebPermissions;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPolicies
{
    /// <summary>
    /// Policy granting read-only access to a calendar.
    /// </summary>
    [Name("calendar_view_policy")]
    [Permission<CalendarReadPermission>()]
    public sealed class CalendarViewPolicy : IIdentityPolicy
    {
    }
}
