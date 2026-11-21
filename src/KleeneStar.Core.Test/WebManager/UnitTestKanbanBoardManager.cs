using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.KanbanBoardManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestKanbanBoardManager
    {
        /// <summary>
        /// Initializes the in-memory database and CoreHub for a single test case.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);
        }

        /// <summary>
        /// Verifies that no board is returned for a workspace/kind pair that was never
        /// customized.
        /// </summary>
        [Fact]
        public void GetBoard_WhenNoneExists_ReturnsNull()
        {
            Seed(nameof(GetBoard_WhenNoneExists_ReturnsNull));

            var board = CoreHub.KanbanBoardManager.GetBoard(Guid.NewGuid(), "issue");

            Assert.Null(board);
        }

        /// <summary>
        /// Verifies that <c>EnsureBoard</c> creates a board on first call and returns the same
        /// board on a second call for the same workspace/kind pair.
        /// </summary>
        [Fact]
        public void EnsureBoard_ReturnsSameBoardOnRepeatedCalls()
        {
            Seed(nameof(EnsureBoard_ReturnsSameBoardOnRepeatedCalls));

            var workspaceId = Guid.NewGuid();

            var first = CoreHub.KanbanBoardManager.EnsureBoard(workspaceId, "issue");
            var second = CoreHub.KanbanBoardManager.EnsureBoard(workspaceId, "issue");

            Assert.Equal(first.Id, second.Id);
        }

        /// <summary>
        /// Verifies that <c>SetColumns</c> persists the desired columns, survives a reload and
        /// raises <see cref="KleeneStar.Core.WebManager.IKanbanBoardManager.BoardUpdated"/>.
        /// </summary>
        [Fact]
        public void SetColumns_PersistsAndRaisesEvent()
        {
            Seed(nameof(SetColumns_PersistsAndRaisesEvent));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(Guid.NewGuid(), "issue");
            var categoryId = Guid.NewGuid();

            KanbanBoard raised = null;
            CoreHub.KanbanBoardManager.BoardUpdated += (_, b) => raised = b;

            CoreHub.KanbanBoardManager.SetColumns(board.Id,
            [
                new KanbanBoardColumn(Guid.Empty) { Name = "To Do", CategoryId = categoryId },
                new KanbanBoardColumn(Guid.Empty) { Name = "Done" }
            ]);

            var loaded = CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind);
            var ordered = loaded.Columns.OrderBy(c => c.Position).ToList();

            Assert.Equal(2, ordered.Count);
            Assert.Equal("To Do", ordered[0].Name);
            Assert.Equal(categoryId, ordered[0].CategoryId);
            Assert.NotNull(raised);
            Assert.Equal(board.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that a column re-submitted with its persisted business id is renamed in
        /// place and that a column omitted from the desired set is removed.
        /// </summary>
        [Fact]
        public void SetColumns_RenamesById_AndDeletesOmitted()
        {
            Seed(nameof(SetColumns_RenamesById_AndDeletesOmitted));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(Guid.NewGuid(), "issue");

            CoreHub.KanbanBoardManager.SetColumns(board.Id,
            [
                new KanbanBoardColumn(Guid.Empty) { Name = "To Do" },
                new KanbanBoardColumn(Guid.Empty) { Name = "Done" }
            ]);

            var toDoId = CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind)
                .Columns.Single(c => c.Name == "To Do").Id;

            CoreHub.KanbanBoardManager.SetColumns(board.Id,
            [
                new KanbanBoardColumn(toDoId) { Name = "Backlog" }
            ]);

            var loaded = CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind);
            var column = Assert.Single(loaded.Columns);

            Assert.Equal(toDoId, column.Id);
            Assert.Equal("Backlog", column.Name);
        }

        /// <summary>
        /// Verifies that <c>SetSwimlanes</c> persists the desired swimlanes and survives a reload.
        /// </summary>
        [Fact]
        public void SetSwimlanes_Persists()
        {
            Seed(nameof(SetSwimlanes_Persists));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(Guid.NewGuid(), "issue");
            var classId = Guid.NewGuid();

            CoreHub.KanbanBoardManager.SetSwimlanes(board.Id,
            [
                new KanbanBoardSwimlane(Guid.Empty) { Name = "Bugs", Color = "#3273A3", ClassId = classId }
            ]);

            var loaded = CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind);
            var swimlane = Assert.Single(loaded.Swimlanes);

            Assert.Equal("Bugs", swimlane.Name);
            Assert.Equal("#3273A3", swimlane.Color);
            Assert.Equal(classId, swimlane.ClassId);
        }

        /// <summary>
        /// Verifies that <c>SetFilter</c> persists the board-level WQL filter and that it can be
        /// cleared again.
        /// </summary>
        [Fact]
        public void SetFilter_PersistsAndClears()
        {
            Seed(nameof(SetFilter_PersistsAndClears));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(Guid.NewGuid(), "issue");

            CoreHub.KanbanBoardManager.SetFilter(board.Id, "Priority = \"P1\"");
            Assert.Equal("Priority = \"P1\"", CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind)?.Filter);

            CoreHub.KanbanBoardManager.SetFilter(board.Id, null);
            Assert.Null(CoreHub.KanbanBoardManager.GetBoard(board.WorkspaceId, board.Kind)?.Filter);
        }
    }
}
