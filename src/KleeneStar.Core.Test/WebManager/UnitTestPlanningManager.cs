using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests personal planning: durations as typed, worklogs that are optional per workspace and
    /// reduce the remaining estimate, worklogs changed only by their author or an administrator,
    /// personal loads against capacity, milestones of one workspace with their progress, and
    /// traces that keep their direction.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPlanningManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("D0D0D000-0000-4000-8000-000000000001");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("D0D0D000-0000-4000-8000-000000000002");
        private static readonly Guid ClassId = Guid.Parse("D0D0D000-0000-4000-8000-000000000003");
        private static readonly Guid AliceId = Guid.Parse("D0D0D000-0000-4000-8000-000000000004");
        private static readonly Guid BobId = Guid.Parse("D0D0D000-0000-4000-8000-000000000005");

        /// <summary>
        /// Seeds two workspaces, a class and two people.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "DEV", Name = "Development" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "OPS", Name = "Operations" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Task", WorkspaceId = WorkspaceId });
            db.Identities.Add(new Identity { Id = AliceId, Name = "Alice", UserName = "alice", Email = "alice@example.com", PasswordHash = "x" });
            db.Identities.Add(new Identity { Id = BobId, Name = "Bob", UserName = "bob", Email = "bob@example.com", PasswordHash = "x" });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates an object.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="key">The key.</param>
        /// <param name="assignee">The assignee, or <see langword="null"/>.</param>
        /// <returns>The object id.</returns>
        private static Guid Object(string database, string key, Guid? assignee = null)
        {
            var id = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Objects.Add(new ObjectEntity(id) { Key = key, Summary = key, WorkspaceId = WorkspaceId, ClassId = ClassId, AssigneeId = assignee });
            db.SaveChanges();

            return id;
        }

        /// <summary>
        /// Durations read the way people type them and write back compactly.
        /// </summary>
        [Fact]
        public void Durations_ReadAndWrite()
        {
            Assert.Equal(90, Durations.Parse("1h 30m"));
            Assert.Equal(90, Durations.Parse("1.5h"));
            Assert.Equal(90, Durations.Parse("1,5h"));
            Assert.Equal(45, Durations.Parse("45"));
            Assert.Equal(Durations.Day * 2, Durations.Parse("2d"));
            Assert.Equal(Durations.Week, Durations.Parse("1w"));
            Assert.Null(Durations.Parse("soon"));
            Assert.Null(Durations.Parse("2h later"));
            Assert.Equal("1d 2h 5m", Durations.Format(Durations.Day + 125));
            Assert.Equal("-", Durations.Format(null));
        }

        /// <summary>
        /// Work is logged only where time tracking is on; a worklog reduces the remaining
        /// estimate unless the person says what is left, and the original estimate stays.
        /// </summary>
        [Fact]
        public void Log_ReducesTheRemainingEstimate()
        {
            var database = nameof(Log_ReducesTheRemainingEstimate);
            Seed(database);

            var objectId = Object(database, "DEV-1");

            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.Log(objectId, AliceId, DateTime.UtcNow, 60, null));

            CoreHub.PlanningManager.SetTimeTracking(WorkspaceId, true);
            CoreHub.PlanningManager.SetEstimate(objectId, 8 * 60, null);

            Assert.Equal(new ObjectEstimate(480, 480, 0, null), CoreHub.PlanningManager.GetEstimate(objectId));

            CoreHub.PlanningManager.Log(objectId, AliceId, DateTime.UtcNow.AddHours(-2), 120, "first draft");
            Assert.Equal(new ObjectEstimate(480, 360, 120, null), CoreHub.PlanningManager.GetEstimate(objectId));

            CoreHub.PlanningManager.Log(objectId, BobId, DateTime.UtcNow.AddHours(-1), 30, "review", remaining: 600);
            Assert.Equal(new ObjectEstimate(480, 600, 150, null), CoreHub.PlanningManager.GetEstimate(objectId));

            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.Log(objectId, AliceId, DateTime.UtcNow, 0, null));
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.Log(objectId, AliceId, DateTime.UtcNow.AddDays(1), 60, null));
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.Log(objectId, Guid.Empty, DateTime.UtcNow, 60, null));
        }

        /// <summary>
        /// Only its author or an administrator changes or removes a worklog.
        /// </summary>
        [Fact]
        public void Worklog_IsChangedByItsAuthorOnly()
        {
            var database = nameof(Worklog_IsChangedByItsAuthorOnly);
            Seed(database);
            CoreHub.PlanningManager.SetTimeTracking(WorkspaceId, true);

            var objectId = Object(database, "DEV-2");
            var worklog = CoreHub.PlanningManager.Log(objectId, AliceId, DateTime.UtcNow.AddHours(-1), 60, null);

            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.UpdateWorklog(worklog.Id, BobId, false, DateTime.UtcNow.AddHours(-1), 90, null));
            Assert.Equal(90, CoreHub.PlanningManager.UpdateWorklog(worklog.Id, AliceId, false, DateTime.UtcNow.AddHours(-1), 90, "longer").Minutes);
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.RemoveWorklog(worklog.Id, BobId, false));

            CoreHub.PlanningManager.RemoveWorklog(worklog.Id, BobId, true);
            Assert.Empty(CoreHub.PlanningManager.GetWorklogs(objectId));
        }

        /// <summary>
        /// A person's load sums the remaining work of their open objects against their capacity
        /// and counts this week's worklogs.
        /// </summary>
        [Fact]
        public void PersonalLoad_WeighsRemainingWorkAgainstCapacity()
        {
            var database = nameof(PersonalLoad_WeighsRemainingWorkAgainstCapacity);
            Seed(database);
            CoreHub.PlanningManager.SetTimeTracking(WorkspaceId, true);

            var first = Object(database, "DEV-3", AliceId);
            Object(database, "DEV-4", AliceId);
            Object(database, "DEV-5", BobId);

            CoreHub.PlanningManager.SetEstimate(first, Durations.Week, null);
            CoreHub.PlanningManager.SetWeeklyCapacity(AliceId, 20 * 60);
            CoreHub.PlanningManager.Log(first, AliceId, DateTime.UtcNow.AddMinutes(-5), 60, null);

            var load = CoreHub.PlanningManager.GetPersonalLoad(AliceId, DateTime.UtcNow);

            Assert.Equal(2, load.Open.Count);
            Assert.Equal(Durations.Week - 60, load.RemainingMinutes);
            Assert.Equal(1, load.Unestimated);
            Assert.Equal(60, load.LoggedThisWeek);
            Assert.Equal(20 * 60, load.WeeklyCapacity);
            Assert.True(load.Weeks > 1.9);

            Assert.Equal(2, CoreHub.PlanningManager.GetTeamLoad(WorkspaceId, DateTime.UtcNow).Count);
            Assert.Equal(PlanningManager.DefaultWeeklyCapacity, CoreHub.PlanningManager.GetWeeklyCapacity(BobId));
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.SetWeeklyCapacity(AliceId, 200 * 60));
        }

        /// <summary>
        /// A milestone holds objects of its own workspace only, reports their progress, and its
        /// removal leaves the objects without a milestone.
        /// </summary>
        [Fact]
        public void Milestone_HoldsObjectsOfItsWorkspace()
        {
            var database = nameof(Milestone_HoldsObjectsOfItsWorkspace);
            Seed(database);

            var release = new Milestone { WorkspaceId = WorkspaceId, Name = "Release 1.0", Due = DateTime.UtcNow.AddDays(30) };
            var foreign = new Milestone { WorkspaceId = OtherWorkspaceId, Name = "Go-live" };

            CoreHub.PlanningManager.SaveMilestone(release).SaveMilestone(foreign);
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.SaveMilestone(new Milestone { WorkspaceId = WorkspaceId, Name = "release 1.0" }));

            var first = Object(database, "DEV-6");
            var second = Object(database, "DEV-7");

            CoreHub.PlanningManager.Assign(first, release.Id);
            CoreHub.PlanningManager.Assign(second, release.Id);
            CoreHub.PlanningManager.SetEstimate(second, 120, null);
            Assert.Throws<PlanningRefusedException>(() => CoreHub.PlanningManager.Assign(first, foreign.Id));

            var progress = CoreHub.PlanningManager.GetProgress(release.Id);

            Assert.Equal(2, progress.Total);
            Assert.Equal(0, progress.Done);
            Assert.Equal(120, progress.RemainingMinutes);
            Assert.Equal(release.Id, CoreHub.PlanningManager.GetEstimate(first).MilestoneId);

            CoreHub.PlanningManager.RemoveMilestone(release.Id);
            Assert.Null(CoreHub.PlanningManager.GetEstimate(first).MilestoneId);
        }

        /// <summary>
        /// A trace runs down from a requirement through task and change to the release, and up
        /// from the release to the requirement - but not sideways to a sibling requirement.
        /// </summary>
        [Fact]
        public void Trace_KeepsItsDirection()
        {
            var database = nameof(Trace_KeepsItsDirection);
            Seed(database);

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "realizes",
                Label = "is realized by",
                InverseLabel = "realizes",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Active = true,
                Order = 1
            });

            var requirement = Object(database, "REQ-1");
            var sibling = Object(database, "REQ-2");
            var task = Object(database, "TASK-1");
            var change = Object(database, "CHG-1");
            var release = Object(database, "REL-1");

            void Relate(Guid source, Guid target) => CoreHub.ObjectRelationManager.Add(new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = "realizes",
                SourceObjectId = source,
                TargetObjectId = target
            });

            Relate(requirement, task);
            Relate(task, change);
            Relate(change, release);
            Relate(sibling, release);

            var down = Traceability.Trace(requirement);

            Assert.Equal(["TASK-1", "CHG-1", "REL-1"], down.Nodes.Where(x => !x.Upstream).Select(x => x.Object.Key));
            Assert.Equal([1, 2, 3], down.Nodes.Select(x => x.Distance));
            Assert.Equal("is realized by", down.Nodes[0].Via);

            var up = Traceability.Trace(release);

            Assert.Equal(new[] { "CHG-1", "REQ-2", "REQ-1", "TASK-1" }.Order(), up.Nodes.Where(x => x.Upstream).Select(x => x.Object.Key).Order());
            Assert.DoesNotContain(down.Nodes, x => x.Object.Key == "REQ-2");
        }
    }
}
