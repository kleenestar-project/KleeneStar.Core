using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests what a workspace grant covers beneath the workspace and which permissions
    /// administer (<see cref="PermissionImplication"/>).
    /// </summary>
    public class UnitTestPermissionImplication
    {
        /// <summary>
        /// Every permission the application declares is read, write, administration or
        /// deliberately on no workspace chain. A permission added tomorrow without a place here
        /// would be neither implied by a workspace grant nor fail closed - it fails this test
        /// instead.
        /// </summary>
        [Fact]
        public void EveryDeclaredPermissionIsClassified()
        {
            var declared = PermissionImplication.Declared().ToList();

            Assert.NotEmpty(declared);
            Assert.All(declared, x => Assert.True(PermissionImplication.IsClassified(x), $"{x.Name} is not classified."));
        }

        /// <summary>
        /// Reading a workspace's content reads its objects and the structure they are drawn
        /// with; writing it changes the objects; administering it administers the structure.
        /// </summary>
        [Fact]
        public void AWorkspacePermissionImpliesTheContentBeneathIt()
        {
            Assert.Contains(typeof(WorkspaceReadContentPermission), PermissionImplication.Satisfying(typeof(ObjectReadPermission)));
            Assert.Contains(typeof(WorkspaceReadContentPermission), PermissionImplication.Satisfying(typeof(FieldReadPermission)));
            Assert.Contains(typeof(WorkspaceWriteContentPermission), PermissionImplication.Satisfying(typeof(ObjectUpdatePermission)));
            Assert.Contains(typeof(WorkspaceWriteContentPermission), PermissionImplication.Satisfying(typeof(TransitionExecutePermission)));
            Assert.Contains(typeof(WorkspaceUpdatePermission), PermissionImplication.Satisfying(typeof(ClassUpdatePermission)));
            Assert.Contains(typeof(WorkspaceUpdatePermission), PermissionImplication.Satisfying(typeof(FormUpdatePermission)));

            // the demanded permission is always accepted itself, first
            Assert.Equal(typeof(ClassUpdatePermission), PermissionImplication.Satisfying(typeof(ClassUpdatePermission)).First());
        }

        /// <summary>
        /// Writing content does not administer: an editor of the workspace is not an
        /// administrator of its classes, which is what keeps the class pages to the admins.
        /// </summary>
        [Fact]
        public void WritingContentDoesNotAdministerTheStructure()
        {
            Assert.DoesNotContain(typeof(WorkspaceWriteContentPermission), PermissionImplication.Satisfying(typeof(ClassUpdatePermission)));
            Assert.DoesNotContain(typeof(WorkspaceReadContentPermission), PermissionImplication.Satisfying(typeof(ObjectUpdatePermission)));
        }

        /// <summary>
        /// Administering fails closed, using does not; a permission on no workspace chain is
        /// neither implied nor administrative.
        /// </summary>
        [Fact]
        public void OnlyAdministrationFailsClosed()
        {
            Assert.True(PermissionImplication.IsAdministrative(typeof(ClassUpdatePermission)));
            Assert.True(PermissionImplication.IsAdministrative(typeof(WorkspaceManageProfilesPermission)));
            Assert.False(PermissionImplication.IsAdministrative(typeof(ObjectReadPermission)));
            Assert.False(PermissionImplication.IsAdministrative(typeof(ObjectUpdatePermission)));
            Assert.False(PermissionImplication.IsAdministrative(typeof(DashboardUpdatePermission)));
            Assert.False(PermissionImplication.IsAdministrative(null));

            Assert.Equal([typeof(DashboardUpdatePermission)], PermissionImplication.Satisfying(typeof(DashboardUpdatePermission)));
        }
    }
}
