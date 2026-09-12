using KleeneStar.Core.Test;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests the guard a page puts in front of what it does
    /// (<see cref="PageAuthorization"/>): it asks the application's own permission rule with
    /// the chain the route names, and refuses by ending the page.
    /// </summary>
    /// <remarks>
    /// The granted path once a chain is administered cannot be decided here: what a policy
    /// carries is resolved through the framework's component registry, which the fixture
    /// wires no host for (see <c>UnitTestPermissionManagerEvaluation</c>). What is decidable
    /// - the unadministered chain, the administered one against an outsider and against an
    /// anonymous caller, and the shape of the chains - is what these cases assert.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestPageAuthorization
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A0700000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("A0700000-0000-0000-0000-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("A0700000-0000-0000-0000-000000000003");
        private static readonly Guid MemberId = Guid.Parse("A0700000-0000-0000-0000-000000000004");
        private static readonly Guid OutsiderId = Guid.Parse("A0700000-0000-0000-0000-000000000005");
        private static readonly Guid GroupId = Guid.Parse("A0700000-0000-0000-0000-000000000006");

        /// <summary>
        /// Seeds a workspace with a class and an object, a group, a member of it and an outsider.
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

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-pa", Name = "guarded" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.Objects.Add(new ObjectEntity(ObjectId) { Key = "WS-1", Summary = "guarded", WorkspaceId = WorkspaceId, ClassId = ClassId });

            var group = new Group { Id = GroupId, Name = "Restorers" };
            db.Groups.Add(group);

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
        /// Writes a grant directly on a resource, bypassing <c>Assign</c> and its catalog check.
        /// </summary>
        private static void Grant(string connectionString, string scope, Guid scopeId, string policy)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.PermissionAssignments.Add(new PermissionAssignment
            {
                Scope = scope,
                ScopeId = scopeId.ToString(),
                GroupId = GroupId,
                Policy = policy,
                Created = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// The object the cases guard, as the page would hold it.
        /// </summary>
        private static ObjectEntity GuardedObject()
        {
            return new ObjectEntity(ObjectId) { WorkspaceId = WorkspaceId, ClassId = ClassId };
        }

        /// <summary>
        /// The chain of a workspace is the workspace; the chain of an object is its class and
        /// then its workspace, never the object itself; nothing at all yields an empty chain.
        /// </summary>
        [Fact]
        public void ChainOf_NamesTheResourcesARouteAdministers()
        {
            var workspace = PageAuthorization.ChainOf(new Workspace { Id = WorkspaceId });
            var @object = PageAuthorization.ChainOf(GuardedObject());

            Assert.Equal(new PermissionResource[] { new(PermissionScope.Workspace, WorkspaceId.ToString()) }, workspace);
            Assert.Equal
            (
                new PermissionResource[]
                {
                    new(PermissionScope.Class, ClassId.ToString()),
                    new(PermissionScope.Workspace, WorkspaceId.ToString())
                },
                @object
            );

            Assert.Empty(PageAuthorization.ChainOf((Workspace)null));
            Assert.Empty(PageAuthorization.ChainOf((ObjectEntity)null));
        }

        /// <summary>
        /// A fresh installation has granted nothing, and a page on such an installation refuses
        /// nobody - not even a caller who is not signed in - because nothing was restricted.
        /// </summary>
        [Fact]
        public void Demand_OnAnUnadministeredChain_LetsEveryoneThrough()
        {
            Seed(nameof(Demand_OnAnUnadministeredChain_LetsEveryoneThrough));

            var chain = PageAuthorization.ChainOf(GuardedObject());

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.True(PageAuthorization.IsGranted(null, typeof(ObjectRestoreStatePermission), chain));
                PageAuthorization.Demand(null, typeof(ObjectRestoreStatePermission), chain);
            }

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.True(PageAuthorization.IsGranted(null, typeof(WorkspaceUpdatePermission), PageAuthorization.ChainOf(new Workspace { Id = WorkspaceId })));
            }
        }

        /// <summary>
        /// One grant on the workspace makes the whole chain administered: a caller outside every
        /// granted group is refused on the object's page as well, and so is an anonymous one.
        /// The refusal is the redirect that ends the page.
        /// </summary>
        [Fact]
        public void Demand_OnAnAdministeredChain_RefusesTheOutsiderAndTheAnonymous()
        {
            Seed(nameof(Demand_OnAnAdministeredChain_RefusesTheOutsiderAndTheAnonymous));
            Grant(nameof(Demand_OnAnAdministeredChain_RefusesTheOutsiderAndTheAnonymous), PermissionScope.Workspace, WorkspaceId, "object_edit_policy");

            var chain = PageAuthorization.ChainOf(GuardedObject());

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.False(PageAuthorization.IsGranted(null, typeof(ObjectRestoreStatePermission), chain));
                Assert.Throws<RedirectException>(() => PageAuthorization.Demand(null, typeof(ObjectRestoreStatePermission), chain));
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.Throws<RedirectException>(() => PageAuthorization.Demand(null, typeof(ObjectRestoreStatePermission), chain));
            }
        }

        /// <summary>
        /// A grant on the class administers the object's chain although the workspace carries
        /// none, because the chain is walked as a whole.
        /// </summary>
        [Fact]
        public void Demand_ReadsAGrantOnTheClassForTheObject()
        {
            Seed(nameof(Demand_ReadsAGrantOnTheClassForTheObject));
            Grant(nameof(Demand_ReadsAGrantOnTheClassForTheObject), PermissionScope.Class, ClassId, "object_edit_policy");

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.False(PageAuthorization.IsGranted(null, typeof(ObjectRestoreStatePermission), PageAuthorization.ChainOf(GuardedObject())));

                // the workspace's own page is still unadministered
                Assert.True(PageAuthorization.IsGranted(null, typeof(WorkspaceUpdatePermission), PageAuthorization.ChainOf(new Workspace { Id = WorkspaceId })));
            }
        }

        /// <summary>
        /// A grant on one workspace says nothing about another: the guard is scoped to what the
        /// route names, which is what a page-level policy attribute could not do.
        /// </summary>
        [Fact]
        public void Demand_IsScopedToTheRoutesResource()
        {
            Seed(nameof(Demand_IsScopedToTheRoutesResource));
            Grant(nameof(Demand_IsScopedToTheRoutesResource), PermissionScope.Workspace, WorkspaceId, "workspace_admin_policy");

            var other = new Workspace { Id = Guid.NewGuid() };

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.False(PageAuthorization.IsGranted(null, typeof(WorkspaceUpdatePermission), PageAuthorization.ChainOf(new Workspace { Id = WorkspaceId })));
                Assert.True(PageAuthorization.IsGranted(null, typeof(WorkspaceUpdatePermission), PageAuthorization.ChainOf(other)));
            }
        }

        /// <summary>
        /// The refusal is a redirect, and it carries no message of its own that a status page
        /// could leak; the page it goes to is the forbidden page.
        /// </summary>
        [Fact]
        public void Refuse_IsARedirect()
        {
            Seed(nameof(Refuse_IsARedirect));

            var refusal = PageAuthorization.Refuse();

            Assert.NotNull(refusal);
            Assert.False(refusal.Permanet);
        }
    }
}
