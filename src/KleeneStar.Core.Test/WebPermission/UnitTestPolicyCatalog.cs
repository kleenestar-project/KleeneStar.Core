using KleeneStar.Core.WebPermission;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Provides unit tests for <see cref="PolicyCatalog"/> — how a registered policy name is
    /// narrowed to a resource and turned into the label the dialog shows.
    /// </summary>
    public class UnitTestPolicyCatalog
    {
        /// <summary>
        /// Verifies that the label names the role rather than repeating the resource the dialog
        /// already belongs to.
        /// </summary>
        /// <param name="policy">The registered policy name.</param>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="expected">The expected label.</param>
        [Theory]
        [InlineData("workspace_admin_policy", PermissionScope.Workspace, "Admin")]
        [InlineData("workspace_view_policy", PermissionScope.Workspace, "View")]
        [InlineData("object_edit_policy", PermissionScope.Object, "Edit")]
        [InlineData("class_exporter_policy", PermissionScope.Class, "Exporter")]
        [InlineData("calendar_admin_policy", PermissionScope.Calendar, "Admin")]
        // the class dialog also administers the objects of the class, and the two admin
        // policies it then offers must not both read "Admin"
        [InlineData("class_admin_policy", PermissionScope.Class, "Admin")]
        [InlineData("object_admin_policy", PermissionScope.Class, "Object admin")]
        [InlineData("object_view_policy", PermissionScope.Class, "Object view")]
        public void GetLabel_NamesTheRole(string policy, string scope, string expected)
        {
            Assert.Equal(expected, PolicyCatalog.GetLabel(policy, scope));
        }

        /// <summary>
        /// Verifies that a name not following the convention is shown as it is rather than being
        /// mangled into something misleading.
        /// </summary>
        [Fact]
        public void GetLabel_LeavesAnUnconventionalNameAlone()
        {
            Assert.Equal("Something", PolicyCatalog.GetLabel("something", PermissionScope.Workspace));
            Assert.Null(PolicyCatalog.GetLabel(null, PermissionScope.Workspace));
        }

        /// <summary>
        /// Verifies that a policy of another resource is not treated as one of this resource's,
        /// which is what keeps a dialog from offering grants its guards never check.
        /// </summary>
        [Fact]
        public void IsKnown_RejectsAPolicyOfAnotherResource()
        {
            Assert.False(PolicyCatalog.IsKnown("workspace_admin_policy", PermissionScope.Object));
        }

        /// <summary>
        /// Verifies that nothing is reported as known without a resource or a name, so a grant
        /// cannot be stored against neither.
        /// </summary>
        /// <param name="policy">The policy name under test.</param>
        /// <param name="scope">The kind of resource under test.</param>
        [Theory]
        [InlineData(null, PermissionScope.Workspace)]
        [InlineData("", PermissionScope.Workspace)]
        [InlineData("workspace_admin_policy", null)]
        [InlineData("workspace_admin_policy", "")]
        public void IsKnown_WithoutAPolicyOrResource_IsFalse(string policy, string scope)
        {
            Assert.False(PolicyCatalog.IsKnown(policy, scope));
        }

        /// <summary>
        /// Verifies that a resource without a registered policy is reported as having none rather
        /// than throwing, which is also the state a unit test runs in.
        /// </summary>
        [Fact]
        public void GetPolicies_WithoutARegistry_ReportsNone()
        {
            Assert.Empty(PolicyCatalog.GetPolicies(PermissionScope.Workspace));
            Assert.Empty(PolicyCatalog.GetPolicies(null));
        }
    }
}
