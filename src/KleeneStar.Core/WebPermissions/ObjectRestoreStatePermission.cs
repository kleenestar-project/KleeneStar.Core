using KleeneStar.Core.WebPolicies;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebPermissions
{
    /// <summary>
    /// Permission authorizing the reapplication of a historical state as a new commit via the
    /// "Restore" button of the history dialog.
    /// </summary>
    /// <remarks>
    /// A restore writes; it is therefore an edit grant rather than a reading one, and it is not
    /// included in the view policy. There is deliberately no permission that edits or removes a
    /// commit — the chain is immutable for every role, which is what makes it usable as evidence.
    /// </remarks>
    [Name("object_restore_state")]
    [Policy<ObjectEditPolicy>()]
    [Policy<ObjectAdminPolicy>()]
    public sealed class ObjectRestoreStatePermission : IIdentityPermission
    {
    }
}
