using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests that the object and workspace reads are narrowed by the caller's grants
    /// (<see cref="ContentVisibility"/>): once, in the manager every read passes through.
    /// </summary>
    /// <remarks>
    /// The positive path - a granted policy that carries the permission - needs the framework's
    /// policy registry, which the fixture wires none of (see
    /// <c>UnitTestPermissionManagerEvaluation</c>). What is decidable without it is asserted:
    /// an administered workspace is closed to a caller outside every granted group, an
    /// unadministered one is open, the system acting on its own is not narrowed, and the
    /// unrestricted scope lifts the filter.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestContentVisibility
    {
        private static readonly Guid OpenWorkspaceId = Guid.Parse("A0730000-0000-0000-0000-000000000001");
        private static readonly Guid GuardedWorkspaceId = Guid.Parse("A0730000-0000-0000-0000-000000000002");
        private static readonly Guid OpenClassId = Guid.Parse("A0730000-0000-0000-0000-000000000003");
        private static readonly Guid GuardedClassId = Guid.Parse("A0730000-0000-0000-0000-000000000004");
        private static readonly Guid OutsiderId = Guid.Parse("A0730000-0000-0000-0000-000000000005");
        private static readonly Guid GroupId = Guid.Parse("A0730000-0000-0000-0000-000000000006");

        /// <summary>
        /// Seeds an open and an administered workspace with one class and one object each.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == OpenWorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = OpenWorkspaceId, Key = "ws-open", Name = "open" });
            db.Workspaces.Add(new Workspace { Id = GuardedWorkspaceId, Key = "ws-guarded", Name = "guarded" });
            db.Classes.Add(new Class { Id = OpenClassId, Name = "Open", WorkspaceId = OpenWorkspaceId });
            db.Classes.Add(new Class { Id = GuardedClassId, Name = "Guarded", WorkspaceId = GuardedWorkspaceId });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "OPEN-1", Summary = "open", WorkspaceId = OpenWorkspaceId, ClassId = OpenClassId });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "GUARD-1", Summary = "guarded", WorkspaceId = GuardedWorkspaceId, ClassId = GuardedClassId });
            db.Groups.Add(new Group { Id = GroupId, Name = "Editors" });
            db.Identities.Add(new Identity { Id = OutsiderId, Name = "Outsider", UserName = "outsider", Email = "outsider@example.com", PasswordHash = "x" });

            // the defaults a template grants - nothing to the anonymous group, nothing to the
            // outsider's groups either
            db.PermissionAssignments.Add(new PermissionAssignment
            {
                Scope = PermissionScope.Workspace,
                ScopeId = GuardedWorkspaceId.ToString(),
                GroupId = GroupId,
                Policy = "workspace_edit_policy",
                Created = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Returns the keys of every object the object manager answers.
        /// </summary>
        /// <returns>The keys.</returns>
        private static List<string> Keys()
        {
            return [.. CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()).Select(x => x.Key).OrderBy(x => x)];
        }

        /// <summary>
        /// A caller outside every group granted on a workspace - signed in or not - does not see
        /// its objects or the workspace in a list; an unadministered workspace stays open.
        /// </summary>
        [Fact]
        public void AnAdministeredWorkspaceIsClosedToTheOutsiderAndTheAnonymous()
        {
            Seed(nameof(AnAdministeredWorkspaceIsClosedToTheOutsiderAndTheAnonymous));

            foreach (var caller in new[] { OutsiderId, Guid.Empty })
            {
                using (CoreHub.SessionManager.BeginIdentity(caller))
                {
                    Assert.Equal(["OPEN-1"], Keys());
                    Assert.Null(CoreHub.ObjectManager.GetObjectByKey("GUARD-1"));
                    Assert.Equal(1, CoreHub.ObjectManager.CountObjects(new Query<ObjectEntity>()));

                    var workspaces = CoreHub.WorkspaceManager.GetWorkspaces(ContentVisibility.Restrict(new Query<Workspace>())).Select(x => x.Id).ToList();

                    Assert.Contains(OpenWorkspaceId, workspaces);
                    Assert.DoesNotContain(GuardedWorkspaceId, workspaces);
                    Assert.False(ContentVisibility.MayRead(GuardedWorkspaceId));
                    Assert.True(ContentVisibility.MayRead(OpenWorkspaceId));
                }
            }
        }

        /// <summary>
        /// Nobody acting is the system acting: without a request and without an identity scope
        /// nothing is narrowed. A read made on the system's behalf inside a caller's scope lifts
        /// the filter with the unrestricted scope.
        /// </summary>
        [Fact]
        public void TheSystemReadsEverything()
        {
            Seed(nameof(TheSystemReadsEverything));

            Assert.Equal(["GUARD-1", "OPEN-1"], Keys());

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            using (CoreHub.SecurityLevelManager.BeginUnrestricted())
            {
                Assert.Equal(["GUARD-1", "OPEN-1"], Keys());
            }
        }
    }
}
