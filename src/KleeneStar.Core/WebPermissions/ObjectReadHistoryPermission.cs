using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission granting read access to an object's commit history and controlling the
    /// visibility of the "History" entry in the object actions menu.
    /// </summary>
    /// <remarks>
    /// Reading the history is a separate grant from reading the object because the two answer
    /// different questions: the object says what is true now, the history says who made it true
    /// and what it replaced. The values inside a commit remain subject to the field-level read
    /// permission, so a field a user cannot see today is not readable through yesterday either.
    /// </remarks>
    [Name("object_read_history")]
    [Policy<ObjectViewPolicy>()]
    [Policy<ObjectEditPolicy>()]
    [Policy<ObjectAdminPolicy>()]
    public sealed class ObjectReadHistoryPermission : IIdentityPermission
    {
    }
}
