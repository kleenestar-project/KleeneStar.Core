using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.SprintManager"/>.
    /// Covers CRUD round-trips, the single-active-sprint rule, sprint assignment of
    /// objects with dense re-ranking, story-point estimation and event emission.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSprintManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("7A1B7C4D-91E2-4F5A-8B3C-D4E5F6A7B8C9");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("8B2C8D5E-A2F3-405B-9C4D-E5F6A7B8C9D0");
        private static readonly Guid ClassId = Guid.Parse("9C3D9E6F-B304-416C-AD5E-F6A7B8C9D0E1");
        private static readonly int[] expected = [1, 2, 3];

        /// <summary>
        /// Seeds the in-memory database with two workspaces and a class the sprint
        /// objects attach to.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-spr", Name = "main" });
            }

            if (!db.Workspaces.Any(x => x.Id == OtherWorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "ws-spr-2", Name = "secondary" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Story", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>AddSprint</c> persists the sprint and that <c>GetSprint</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void AddSprint_Then_GetSprint_RoundTrip()
        {
            Seed(nameof(AddSprint_Then_GetSprint_RoundTrip));

            var sprint = SampleSprint("Sprint 1");
            CoreHub.SprintManager.AddSprint(sprint);

            var loaded = CoreHub.SprintManager.GetSprint(sprint.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Sprint 1", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetSprintsForWorkspace</c> returns only sprints of the
        /// supplied workspace, ordered by name.
        /// </summary>
        [Fact]
        public void GetSprintsForWorkspace_ReturnsOnlyOwnWorkspaceOrderedByName()
        {
            Seed(nameof(GetSprintsForWorkspace_ReturnsOnlyOwnWorkspaceOrderedByName));

            CoreHub.SprintManager.AddSprint(SampleSprint("Sprint 2"));
            CoreHub.SprintManager.AddSprint(SampleSprint("Sprint 1"));
            CoreHub.SprintManager.AddSprint(SampleSprint("Sprint 9", workspaceId: OtherWorkspaceId));

            var result = CoreHub.SprintManager.GetSprintsForWorkspace(WorkspaceId).ToList();

            Assert.Equal(2, result.Count);
            Assert.Equal("Sprint 1", result[0].Name);
            Assert.Equal("Sprint 2", result[1].Name);
        }

        /// <summary>
        /// Verifies that <c>UpdateSprint</c> writes scalar property changes back.
        /// </summary>
        [Fact]
        public void UpdateSprint_ChangesScalars()
        {
            Seed(nameof(UpdateSprint_ChangesScalars));

            var sprint = SampleSprint("Initial");
            CoreHub.SprintManager.AddSprint(sprint);

            sprint.Name = "Renamed";
            sprint.Goal = "Ship it";
            sprint.Capacity = 55;
            CoreHub.SprintManager.UpdateSprint(sprint);

            var loaded = CoreHub.SprintManager.GetSprint(sprint.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal("Ship it", loaded.Goal);
            Assert.Equal(55, loaded.Capacity);
        }

        /// <summary>
        /// Verifies that activating a sprint completes every other active sprint of the
        /// same workspace, while active sprints of other workspaces stay untouched.
        /// </summary>
        [Fact]
        public void UpdateSprint_Activation_CompletesOtherActiveSprints()
        {
            Seed(nameof(UpdateSprint_Activation_CompletesOtherActiveSprints));

            var running = SampleSprint("Sprint 1", state: SprintState.Active);
            var next = SampleSprint("Sprint 2");
            var foreign = SampleSprint("Sprint 9", state: SprintState.Active, workspaceId: OtherWorkspaceId);
            CoreHub.SprintManager.AddSprint(running);
            CoreHub.SprintManager.AddSprint(next);
            CoreHub.SprintManager.AddSprint(foreign);

            next.State = SprintState.Active;
            CoreHub.SprintManager.UpdateSprint(next);

            Assert.Equal(SprintState.Completed, CoreHub.SprintManager.GetSprint(running.Id).State);
            Assert.Equal(SprintState.Active, CoreHub.SprintManager.GetSprint(next.Id).State);
            Assert.Equal(SprintState.Active, CoreHub.SprintManager.GetSprint(foreign.Id).State);
            Assert.Equal(next.Id, CoreHub.SprintManager.GetActiveSprint(WorkspaceId)?.Id);
        }

        /// <summary>
        /// Verifies that <c>MoveObjectToSprint</c> commits an object to a sprint,
        /// appends it at the end of the target group and re-ranks the backlog it left.
        /// </summary>
        [Fact]
        public void MoveObjectToSprint_AppendsAndReRanksSource()
        {
            Seed(nameof(MoveObjectToSprint_AppendsAndReRanksSource));

            var sprint = SampleSprint("Sprint 1");
            CoreHub.SprintManager.AddSprint(sprint);

            var first = SampleObject("SPR-1", 1);
            var second = SampleObject("SPR-2", 2);
            var third = SampleObject("SPR-3", 3);
            CoreHub.ObjectManager.Add(first);
            CoreHub.ObjectManager.Add(second);
            CoreHub.ObjectManager.Add(third);

            CoreHub.SprintManager.MoveObjectToSprint(second.Id, sprint.Id);

            var committed = CoreHub.SprintManager.GetSprintObjects(WorkspaceId, sprint.Id);
            var backlog = CoreHub.SprintManager.GetSprintObjects(WorkspaceId, null);

            Assert.Single(committed);
            Assert.Equal(second.Id, committed[0].Id);
            Assert.Equal(1, committed[0].SprintRank);

            Assert.Equal(2, backlog.Count);
            Assert.Equal(first.Id, backlog[0].Id);
            Assert.Equal(1, backlog[0].SprintRank);
            Assert.Equal(third.Id, backlog[1].Id);
            Assert.Equal(2, backlog[1].SprintRank);
        }

        /// <summary>
        /// Verifies that <c>MoveObjectToSprint</c> with an explicit rank inserts the
        /// object at that position and keeps the group ranks dense and 1-based.
        /// </summary>
        [Fact]
        public void MoveObjectToSprint_WithRank_InsertsAtPosition()
        {
            Seed(nameof(MoveObjectToSprint_WithRank_InsertsAtPosition));

            var sprint = SampleSprint("Sprint 1");
            CoreHub.SprintManager.AddSprint(sprint);

            var first = SampleObject("SPR-1", 1);
            var second = SampleObject("SPR-2", 2);
            var third = SampleObject("SPR-3", 3);
            CoreHub.ObjectManager.Add(first);
            CoreHub.ObjectManager.Add(second);
            CoreHub.ObjectManager.Add(third);

            CoreHub.SprintManager.MoveObjectToSprint(first.Id, sprint.Id);
            CoreHub.SprintManager.MoveObjectToSprint(second.Id, sprint.Id);
            CoreHub.SprintManager.MoveObjectToSprint(third.Id, sprint.Id, 1);

            var committed = CoreHub.SprintManager.GetSprintObjects(WorkspaceId, sprint.Id);

            Assert.Equal(3, committed.Count);
            Assert.Equal(third.Id, committed[0].Id);
            Assert.Equal(first.Id, committed[1].Id);
            Assert.Equal(second.Id, committed[2].Id);
            Assert.Equal(expected, committed.Select(x => x.SprintRank).ToArray());
        }

        /// <summary>
        /// Verifies that <c>SetStoryPoints</c> persists and clears the estimate.
        /// </summary>
        [Fact]
        public void SetStoryPoints_PersistsAndClears()
        {
            Seed(nameof(SetStoryPoints_PersistsAndClears));

            var entity = SampleObject("SPR-1", 1);
            CoreHub.ObjectManager.Add(entity);

            CoreHub.SprintManager.SetStoryPoints(entity.Id, 8);
            Assert.Equal(8, CoreHub.ObjectManager.GetObject(entity.Id).StoryPoints);

            CoreHub.SprintManager.SetStoryPoints(entity.Id, null);
            Assert.Null(CoreHub.ObjectManager.GetObject(entity.Id).StoryPoints);
        }

        /// <summary>
        /// Verifies that <c>RemoveSprint</c> deletes the sprint, moves its objects back
        /// to the backlog behind the existing backlog items, and raises the
        /// <see cref="KleeneStar.Core.WebManager.ISprintManager.SprintRemoved"/> event.
        /// </summary>
        [Fact]
        public void RemoveSprint_MovesObjectsToBacklogAndRaisesEvent()
        {
            Seed(nameof(RemoveSprint_MovesObjectsToBacklogAndRaisesEvent));

            var sprint = SampleSprint("Sprint 1");
            CoreHub.SprintManager.AddSprint(sprint);

            var committed = SampleObject("SPR-1", 1);
            var waiting = SampleObject("SPR-2", 1);
            CoreHub.ObjectManager.Add(committed);
            CoreHub.ObjectManager.Add(waiting);
            CoreHub.SprintManager.MoveObjectToSprint(committed.Id, sprint.Id);

            Sprint raised = null;
            CoreHub.SprintManager.SprintRemoved += (_, s) => raised = s;

            CoreHub.SprintManager.RemoveSprint(sprint);

            Assert.Null(CoreHub.SprintManager.GetSprint(sprint.Id));
            Assert.NotNull(raised);
            Assert.Equal(sprint.Id, raised.Id);

            var backlog = CoreHub.SprintManager.GetSprintObjects(WorkspaceId, null);
            Assert.Equal(2, backlog.Count);
            Assert.Equal(waiting.Id, backlog[0].Id);
            Assert.Equal(committed.Id, backlog[1].Id);
            Assert.Equal(2, backlog[1].SprintRank);
        }

        /// <summary>
        /// Creates a sample sprint attached to one of the seeded workspaces.
        /// </summary>
        /// <param name="name">The sprint name.</param>
        /// <param name="state">The lifecycle state.</param>
        /// <param name="workspaceId">The workspace to attach to; defaults to the primary workspace.</param>
        /// <returns>A new sprint with a fresh GUID.</returns>
        private static Sprint SampleSprint(string name, SprintState state = SprintState.Planned, Guid? workspaceId = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Goal = $"Goal of {name}.",
            State = state,
            Capacity = 40,
            WorkspaceId = workspaceId ?? WorkspaceId,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };

        /// <summary>
        /// Creates a sample backlog object attached to the primary workspace.
        /// </summary>
        /// <param name="key">The unique object key.</param>
        /// <param name="rank">The initial backlog rank.</param>
        /// <returns>A new object with a fresh GUID.</returns>
        private static ObjectEntity SampleObject(string key, int rank) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Summary = $"Summary of {key}",
            State = WorkspaceState.Active,
            WorkspaceId = WorkspaceId,
            ClassId = ClassId,
            SprintRank = rank,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow
        };
    }
}
