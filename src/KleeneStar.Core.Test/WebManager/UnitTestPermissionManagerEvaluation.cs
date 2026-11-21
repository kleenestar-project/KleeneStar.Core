using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the permission evaluation of
    /// <see cref="KleeneStar.Core.WebManager.PermissionManager"/>: whether an identity holds a
    /// permission on a resource, and what an unadministered resource answers.
    /// </summary>
    /// <remarks>
    /// The positive path - a granted policy that actually carries the permission - cannot be
    /// decided here: resolving what a policy carries is a question for the framework's component
    /// registry, and the fixture wires the managers without one on purpose. What is decidable
    /// without a host is exactly what these tests assert; the granted path is verified against the
    /// running application instead.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestPermissionManagerEvaluation
    {
        private static readonly Guid WorkspaceId = Guid.Parse("22220000-0000-0000-0000-000000000001");
        private static readonly Guid MemberId = Guid.Parse("22220000-0000-0000-0000-000000000002");
        private static readonly Guid OutsiderId = Guid.Parse("22220000-0000-0000-0000-000000000003");
        private static readonly Guid GroupId = Guid.Parse("22220000-0000-0000-0000-000000000004");

        /// <summary>
        /// Seeds a workspace, a group, an identity in that group and one outside it.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-perm", Name = "perm" });

            var group = new Group { Id = GroupId, Name = "Editors" };
            db.Groups.Add(group);

            // the membership is written through the navigation, which is how the seeder does it
            // too - the join carries database ids that do not exist before the insert
            db.Identities.Add(new Identity
            {
                Id = MemberId,
                Name = "Member",
                UserName = "member",
                Email = "member@example.com",
                PasswordHash = "x",
                GroupMemberships = [new IdentityGroupMembership { Group = group }]
            });

            db.Identities.Add(new Identity { Id = OutsiderId, Name = "Outsider", UserName = "outsider", Email = "outsider@example.com", PasswordHash = "x" });

            db.SaveChanges();
        }

        /// <summary>
        /// Returns the resource chain of the seeded workspace.
        /// </summary>
        /// <returns>The chain.</returns>
        private static PermissionResource[] Chain()
        {
            return [new PermissionResource(PermissionScope.Workspace, WorkspaceId.ToString())];
        }

        /// <summary>
        /// Verifies that a resource nobody has administered is not a forbidden one. Reading
        /// "nobody said yes" as "everybody is refused" would make every record unreachable the
        /// moment a guard is put in front of it.
        /// </summary>
        [Fact]
        public void IsGranted_WithoutAnyGrant_Allows()
        {
            Seed(nameof(IsGranted_WithoutAnyGrant_Allows));

            Assert.True(CoreHub.PermissionManager.IsGranted(MemberId, typeof(ObjectRelationPermission), Chain()));
            Assert.True(CoreHub.PermissionManager.IsGranted(OutsiderId, typeof(ObjectRelationPermission), Chain()));
        }

        /// <summary>
        /// Verifies that a single grant makes the chain administered, after which an identity that
        /// belongs to no granted group is refused.
        /// </summary>
        [Fact]
        public void IsGranted_OnceAdministered_RefusesAnIdentityOutsideEveryGrantedGroup()
        {
            Seed(nameof(IsGranted_OnceAdministered_RefusesAnIdentityOutsideEveryGrantedGroup));

            Grant();

            Assert.False(CoreHub.PermissionManager.IsGranted(OutsiderId, typeof(ObjectRelationPermission), Chain()));
        }

        /// <summary>
        /// Verifies that an unresolvable caller is refused once the chain is administered. Before
        /// that it is allowed, because nothing was restricted - the two readings must not be
        /// confused.
        /// </summary>
        [Fact]
        public void IsGranted_OnceAdministered_RefusesAnUnknownIdentity()
        {
            Seed(nameof(IsGranted_OnceAdministered_RefusesAnUnknownIdentity));

            Assert.True(CoreHub.PermissionManager.IsGranted(Guid.Empty, typeof(ObjectRelationPermission), Chain()));

            Grant();

            Assert.False(CoreHub.PermissionManager.IsGranted(Guid.Empty, typeof(ObjectRelationPermission), Chain()));
        }

        /// <summary>
        /// Verifies that a resource chain the caller states as empty, or one whose links resolve to
        /// nothing, is treated as unadministered rather than as refused - a route naming nothing is
        /// not an authorization decision.
        /// </summary>
        [Fact]
        public void IsGranted_WithUnresolvableChain_Allows()
        {
            Seed(nameof(IsGranted_WithUnresolvableChain_Allows));

            Grant();

            Assert.True(CoreHub.PermissionManager.IsGranted(OutsiderId, typeof(ObjectRelationPermission)));
            Assert.True(CoreHub.PermissionManager.IsGranted(OutsiderId, typeof(ObjectRelationPermission), new PermissionResource(PermissionScope.Workspace, null)));
        }

        /// <summary>
        /// Verifies that a grant naming a policy the running system does not know carries nothing.
        /// It was written against a component that is gone, and reading it as a grant of everything
        /// would turn an uninstalled plugin into an escalation.
        /// </summary>
        [Fact]
        public void IsGranted_WithAPolicyTheSystemDoesNotKnow_Refuses()
        {
            Seed(nameof(IsGranted_WithAPolicyTheSystemDoesNotKnow_Refuses));

            Store("workspace_from_an_uninstalled_plugin_policy");

            Assert.False(CoreHub.PermissionManager.IsGranted(MemberId, typeof(ObjectRelationPermission), Chain()));
        }

        /// <summary>
        /// Verifies that a check without a permission to test is not an authorization question.
        /// </summary>
        [Fact]
        public void IsGranted_WithoutAPermission_Allows()
        {
            Seed(nameof(IsGranted_WithoutAPermission_Allows));

            Grant();

            Assert.True(CoreHub.PermissionManager.IsGranted(OutsiderId, null, Chain()));
        }

        /// <summary>
        /// Grants the seeded group an object policy on the workspace, which is what makes the
        /// chain administered.
        /// </summary>
        private static void Grant()
        {
            Store("workspace_admin_policy");
        }

        /// <summary>
        /// Writes a grant directly, bypassing <c>Assign</c>, which validates the policy against the
        /// catalog the running host publishes and would refuse every name without one.
        /// </summary>
        /// <param name="policy">The policy name to record.</param>
        private static void Store(string policy)
        {
            using var db = CoreHubFixture.CreateDbContext(TestDatabase());

            db.PermissionAssignments.Add(new PermissionAssignment
            {
                Id = Guid.NewGuid(),
                Scope = PermissionScope.Workspace,
                ScopeId = WorkspaceId.ToString(),
                GroupId = GroupId,
                Policy = policy,
                Created = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Returns the in-memory database the current test was initialized with.
        /// </summary>
        /// <returns>The connection string.</returns>
        private static string TestDatabase()
        {
            return KleeneStar.Model.ModelHub.DatabaseSettings.ConnectionString;
        }
    }
}
