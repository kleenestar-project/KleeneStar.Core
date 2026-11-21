using KleeneStar.Core.Test;
using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.PermissionManager"/> — the
    /// group-to-policy grants the permission dialogs administer.
    /// </summary>
    /// <remarks>
    /// The catalog of policies comes from the running application's registered components, which a
    /// unit test has none of, so <see cref="PolicyCatalog.IsKnown"/> reports nothing as known here.
    /// These tests therefore cover what the manager decides on its own — scoping, duplicates and
    /// withdrawal — and the guard against unknown policies is asserted as exactly that.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestPermissionManager
    {
        private static readonly Guid GroupId = Guid.Parse("4D5E6F70-8192-43A4-B5C6-D7E8F9012345");

        /// <summary>
        /// Initializes the hub and seeds the group the policies are granted to.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Groups.Any(x => x.Id == GroupId))
            {
                db.Groups.Add(new Group
                {
                    Id = GroupId,
                    Name = "Engineering",
                    Description = "Grant target"
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Stores a grant directly, bypassing the catalog check the manager performs, so the
        /// reading and withdrawing paths can be exercised without a registered policy.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The resource.</param>
        /// <param name="policy">The policy name.</param>
        private static void Grant(string connectionString, string scope, string scopeId, string policy)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.PermissionAssignments.Add(new PermissionAssignment(Guid.NewGuid())
            {
                Scope = scope,
                ScopeId = scopeId,
                GroupId = GroupId,
                Policy = policy,
                Created = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that the grants of one resource are reported and those of another are not.
        /// </summary>
        [Fact]
        public void GetAssignments_IsScopedToTheResource()
        {
            var name = nameof(GetAssignments_IsScopedToTheResource);
            Seed(name);

            Grant(name, PermissionScope.Workspace, "ws-1", "workspace_admin_policy");
            Grant(name, PermissionScope.Workspace, "ws-2", "workspace_view_policy");

            var assignments = CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, "ws-1").ToList();

            Assert.Single(assignments);
            Assert.Equal("workspace_admin_policy", assignments[0].Policy);
            Assert.Equal("Engineering", assignments[0].Group?.Name);
        }

        /// <summary>
        /// Verifies that the same resource id under a different kind of resource is a different
        /// resource, so an object and a workspace that happen to share an id do not share grants.
        /// </summary>
        [Fact]
        public void GetAssignments_IsScopedToTheKindOfResource()
        {
            var name = nameof(GetAssignments_IsScopedToTheKindOfResource);
            Seed(name);

            Grant(name, PermissionScope.Workspace, "same-id", "workspace_admin_policy");

            Assert.Single(CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, "same-id"));
            Assert.Empty(CoreHub.PermissionManager.GetAssignments(PermissionScope.Object, "same-id"));
        }

        /// <summary>
        /// Verifies that a resource without grants reports none rather than everything.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetAssignments_WithoutAResource_ReportsNone(string scopeId)
        {
            var name = nameof(GetAssignments_WithoutAResource_ReportsNone) + scopeId?.Length;
            Seed(name);

            Grant(name, PermissionScope.Workspace, "ws-1", "workspace_admin_policy");

            Assert.Empty(CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, scopeId));
        }

        /// <summary>
        /// Verifies that a grant naming a policy the application did not register is refused, so
        /// the list cannot show a grant no guard would ever honour.
        /// </summary>
        [Fact]
        public void Assign_RefusesAnUnregisteredPolicy()
        {
            var name = nameof(Assign_RefusesAnUnregisteredPolicy);
            Seed(name);

            Assert.Null(CoreHub.PermissionManager.Assign(PermissionScope.Workspace, "ws-1", GroupId, "no_such_policy"));
            Assert.Empty(CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, "ws-1"));
        }

        /// <summary>
        /// Verifies that a grant to a group that does not exist is refused.
        /// </summary>
        [Fact]
        public void Assign_RefusesAnUnknownGroup()
        {
            var name = nameof(Assign_RefusesAnUnknownGroup);
            Seed(name);

            Assert.Null(CoreHub.PermissionManager.Assign(PermissionScope.Workspace, "ws-1", Guid.NewGuid(), "workspace_admin_policy"));
        }

        /// <summary>
        /// Verifies that a grant is withdrawn, and that withdrawing what was never granted is
        /// reported as such rather than throwing.
        /// </summary>
        [Fact]
        public void Revoke_RemovesTheGrantAndReportsWhenThereWasNone()
        {
            var name = nameof(Revoke_RemovesTheGrantAndReportsWhenThereWasNone);
            Seed(name);

            Grant(name, PermissionScope.Workspace, "ws-1", "workspace_admin_policy");

            Assert.True(CoreHub.PermissionManager.Revoke(PermissionScope.Workspace, "ws-1", GroupId, "workspace_admin_policy"));
            Assert.Empty(CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, "ws-1"));

            Assert.False(CoreHub.PermissionManager.Revoke(PermissionScope.Workspace, "ws-1", GroupId, "workspace_admin_policy"));
        }

        /// <summary>
        /// Verifies that withdrawing a grant leaves the other grants on the resource alone.
        /// </summary>
        [Fact]
        public void Revoke_LeavesTheOtherGrantsAlone()
        {
            var name = nameof(Revoke_LeavesTheOtherGrantsAlone);
            Seed(name);

            Grant(name, PermissionScope.Workspace, "ws-1", "workspace_admin_policy");
            Grant(name, PermissionScope.Workspace, "ws-1", "workspace_view_policy");

            CoreHub.PermissionManager.Revoke(PermissionScope.Workspace, "ws-1", GroupId, "workspace_admin_policy");

            var remaining = CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, "ws-1").ToList();

            Assert.Single(remaining);
            Assert.Equal("workspace_view_policy", remaining[0].Policy);
        }
    }
}
