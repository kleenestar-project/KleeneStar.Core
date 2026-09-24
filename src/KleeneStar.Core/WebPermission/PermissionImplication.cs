using KleeneStar.Core.WebPermissions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Says what a workspace grant covers beneath the workspace, and which permissions
    /// administer rather than use.
    /// </summary>
    /// <remarks>
    /// The workspace policies carry only <c>workspace_…</c> permissions, while the guards of
    /// the records inside a workspace ask for <c>object_…</c>, <c>class_…</c>, <c>field_…</c> and
    /// the like. Without a bridge, the first grant on a workspace would lock everybody out of
    /// its content - the workspace's own administrator included - because no workspace policy
    /// carries <see cref="ObjectReadPermission"/>. The bridge is three sentences, and this
    /// class is where they are written down once:
    /// <list type="bullet">
    /// <item><see cref="WorkspaceReadContentPermission"/> reads the content: the objects, their
    /// history, and the structure their pages need to be drawn (class, fields, forms,
    /// workflows, priorities, statuses, security levels, calendars).</item>
    /// <item><see cref="WorkspaceWriteContentPermission"/> changes the content: edits,
    /// comments, attachments, relations, transitions, restoring a revision.</item>
    /// <item><see cref="WorkspaceUpdatePermission"/> - held by the workspace administrators
    /// alone - administers the structure: every write of a class, field, form, workflow,
    /// priority, status, security level or calendar, and the permission dialogs.</item>
    /// </list>
    /// A grant on the class itself (<c>class_…</c>, <c>object_…</c>) still answers directly;
    /// the bridge only adds the workspace's say.
    /// <para>
    /// <b>Administration fails closed.</b> An unadministered chain is open for using what is on
    /// it - the installation said nothing, see <c>IPermissionManager.IsGranted</c> - but not for
    /// administering it: there, the members of <c>Group.AdministratorsId</c> are the
    /// administrators, the way they are of the accounts. Otherwise a fresh or legacy workspace
    /// would hand its class administration to every anonymous visitor.
    /// </para>
    /// </remarks>
    public static class PermissionImplication
    {
        /// <summary>
        /// The permissions reading a workspace's content implies.
        /// </summary>
        private static readonly HashSet<Type> ContentRead =
        [
            typeof(WorkspaceReadPermission),
            typeof(ObjectReadPermission),
            typeof(ObjectReadHistoryPermission),
            typeof(ClassReadPermission),
            typeof(FieldReadPermission),
            typeof(FieldReadValuesPermission),
            typeof(FormReadPermission),
            typeof(PriorityReadPermission),
            typeof(StatusReadPermission),
            typeof(StatusUsageReadPermission),
            typeof(WorkflowReadPermission),
            typeof(WorkflowVersionsReadPermission),
            typeof(SecurityLevelReadPermission),
            typeof(CalendarReadPermission)
        ];

        /// <summary>
        /// The permissions writing a workspace's content implies.
        /// </summary>
        private static readonly HashSet<Type> ContentWrite =
        [
            typeof(ObjectUpdatePermission),
            typeof(ObjectCommentPermission),
            typeof(ObjectAttachPermission),
            typeof(ObjectRelationPermission),
            typeof(ObjectRestoreStatePermission),
            typeof(TransitionExecutePermission),
            typeof(FieldWriteValuesPermission)
        ];

        /// <summary>
        /// The permissions that administer a workspace and the structure of its classes.
        /// </summary>
        private static readonly HashSet<Type> Administration =
        [
            typeof(WorkspaceCreatePermission),
            typeof(WorkspaceUpdatePermission),
            typeof(WorkspaceDeletePermission),
            typeof(WorkspaceArchivePermission),
            typeof(WorkspaceRestorePermission),
            typeof(WorkspaceClonePermission),
            typeof(WorkspaceManageProfilesPermission),
            typeof(ClassCreatePermission),
            typeof(ClassUpdatePermission),
            typeof(ClassDeletePermission),
            typeof(ClassClonePermission),
            typeof(ClassImportPermission),
            typeof(ClassExportPermission),
            typeof(ClassManagePermissionsPermission),
            typeof(FieldCreatePermission),
            typeof(FieldUpdatePermission),
            typeof(FieldDeletePermission),
            typeof(FieldArchivePermission),
            typeof(FieldRestorePermission),
            typeof(FieldClonePermission),
            typeof(FieldManagePermissionsPermission),
            typeof(FormCreatePermission),
            typeof(FormUpdatePermission),
            typeof(FormDeletePermission),
            typeof(FormArchivePermission),
            typeof(FormRestorePermission),
            typeof(FormClonePermission),
            typeof(FormImportPermission),
            typeof(FormExportPermission),
            typeof(FormAssignTransitionPermission),
            typeof(PriorityCreatePermission),
            typeof(PriorityUpdatePermission),
            typeof(PriorityDeletePermission),
            typeof(PriorityArchivePermission),
            typeof(PriorityRestorePermission),
            typeof(PriorityClonePermission),
            typeof(PriorityImportPermission),
            typeof(PriorityExportPermission),
            typeof(StatusCreatePermission),
            typeof(StatusUpdatePermission),
            typeof(StatusDeletePermission),
            typeof(StatusClonePermission),
            typeof(WorkflowUpdatePermission),
            typeof(WorkflowDeletePermission),
            typeof(WorkflowArchivePermission),
            typeof(WorkflowRestorePermission),
            typeof(WorkflowClonePermission),
            typeof(WorkflowImportPermission),
            typeof(WorkflowExportPermission),
            typeof(WorkflowManagePermissionsPermission),
            typeof(WorkflowPublishPermission),
            typeof(WorkflowValidatePermission),
            typeof(SecurityLevelCreatePermission),
            typeof(SecurityLevelUpdatePermission),
            typeof(SecurityLevelDeletePermission),
            typeof(SecurityLevelClonePermission),
            typeof(CalendarUpdatePermission),
            typeof(CalendarDeletePermission),
            typeof(ObjectManageProfilesPermission)
        ];

        /// <summary>
        /// The permissions that are on no workspace chain at all - the installation's dashboards,
        /// groups and accounts - and are therefore neither implied nor classified.
        /// </summary>
        private static readonly HashSet<Type> Unscoped =
        [
            typeof(DashboardArchivePermission),
            typeof(DashboardClonePermission),
            typeof(DashboardCreatePermission),
            typeof(DashboardDeletePermission),
            typeof(DashboardManageProfilesPermission),
            typeof(DashboardReadContentPermission),
            typeof(DashboardReadPermission),
            typeof(DashboardRestorePermission),
            typeof(DashboardUpdatePermission),
            typeof(DashboardWriteContentPermission),
            typeof(GroupCreatePermission),
            typeof(GroupDeletePermission),
            typeof(GroupReadPermission),
            typeof(GroupUpdatePermission),
            typeof(IdentityCreatePermission),
            typeof(IdentityDeletePermission),
            typeof(IdentityReadPermission),
            typeof(IdentityUpdatePermission)
        ];

        /// <summary>
        /// Determines whether a permission administers rather than uses - and is therefore
        /// refused on an unadministered chain to everybody but the installation's administrators.
        /// </summary>
        /// <param name="permission">The permission type.</param>
        /// <returns><see langword="true"/> for an administrative permission.</returns>
        public static bool IsAdministrative(Type permission)
        {
            return permission is not null && Administration.Contains(permission);
        }

        /// <summary>
        /// Determines whether a permission is classified at all - read, write, administration or
        /// deliberately unscoped. A test keeps every permission the application declares in one of them.
        /// </summary>
        /// <param name="permission">The permission type.</param>
        /// <returns><see langword="true"/> when the permission is classified.</returns>
        public static bool IsClassified(Type permission)
        {
            return ContentRead.Contains(permission)
                || ContentWrite.Contains(permission)
                || Administration.Contains(permission)
                || Unscoped.Contains(permission)
                || permission == typeof(WorkspaceReadContentPermission)
                || permission == typeof(WorkspaceWriteContentPermission);
        }

        /// <summary>
        /// Returns the permissions any one of which satisfies a demand for the supplied one: the
        /// permission itself, and the workspace permission that implies it.
        /// </summary>
        /// <param name="permission">The permission demanded.</param>
        /// <returns>The accepted permissions, the demanded one first.</returns>
        public static IEnumerable<Type> Satisfying(Type permission)
        {
            if (permission is null)
            {
                return [];
            }

            var implied = ContentRead.Contains(permission) ? typeof(WorkspaceReadContentPermission)
                : ContentWrite.Contains(permission) ? typeof(WorkspaceWriteContentPermission)
                : Administration.Contains(permission) ? typeof(WorkspaceUpdatePermission)
                : null;

            return implied is null || implied == permission
                ? [permission]
                : [permission, implied];
        }

        /// <summary>
        /// Returns every permission the application declares, for the classification test.
        /// </summary>
        /// <returns>The permission types of this assembly.</returns>
        public static IEnumerable<Type> Declared()
        {
            return typeof(PermissionImplication).Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && x.Namespace == typeof(ObjectReadPermission).Namespace
                    && x.Name.EndsWith("Permission", StringComparison.Ordinal));
        }
    }
}
