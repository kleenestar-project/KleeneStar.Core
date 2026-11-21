using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.KindDashboardManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestKindDashboardManager
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

            var board = CoreHub.KindDashboardManager.GetBoard(Guid.NewGuid(), "issue");

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

            var first = CoreHub.KindDashboardManager.EnsureBoard(workspaceId, "issue");
            var second = CoreHub.KindDashboardManager.EnsureBoard(workspaceId, "issue");

            Assert.Equal(first.Id, second.Id);
        }

        /// <summary>
        /// Verifies that <c>SetBoard</c> persists columns with their widgets, survives a
        /// reload, and raises
        /// <see cref="KleeneStar.Core.WebManager.IKindDashboardManager.BoardUpdated"/>.
        /// </summary>
        [Fact]
        public void SetBoard_PersistsWidgetsAndRaisesEvent()
        {
            Seed(nameof(SetBoard_PersistsWidgetsAndRaisesEvent));

            var board = CoreHub.KindDashboardManager.EnsureBoard(Guid.NewGuid(), "issue");

            KindDashboard raised = null;
            CoreHub.KindDashboardManager.BoardUpdated += (_, b) => raised = b;

            CoreHub.KindDashboardManager.SetBoard(board.Id,
            [
                new KindDashboardColumn(Guid.Empty)
                {
                    Name = "Total",
                    Widgets = [new KindDashboardWidget(Guid.NewGuid()) { Type = "widget_bignumber", Name = "Total" }]
                }
            ]);

            var loaded = CoreHub.KindDashboardManager.GetBoard(board.WorkspaceId, board.Kind);
            var column = Assert.Single(loaded.Columns);

            Assert.Equal("Total", column.Name);
            Assert.Single(column.Widgets);
            Assert.NotNull(raised);
            Assert.Equal(board.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>SetColumns</c> renames a column in place by its persisted business
        /// id and deletes a column omitted from the desired set, together with its widgets.
        /// </summary>
        [Fact]
        public void SetColumns_RenamesById_AndDeletesOmitted()
        {
            Seed(nameof(SetColumns_RenamesById_AndDeletesOmitted));

            var board = CoreHub.KindDashboardManager.EnsureBoard(Guid.NewGuid(), "issue");

            CoreHub.KindDashboardManager.SetBoard(board.Id,
            [
                new KindDashboardColumn(Guid.Empty)
                {
                    Name = "Total",
                    Widgets = [new KindDashboardWidget(Guid.NewGuid()) { Type = "widget_bignumber", Name = "Total" }]
                },
                new KindDashboardColumn(Guid.Empty) { Name = "Active" }
            ]);

            var totalId = CoreHub.KindDashboardManager.GetBoard(board.WorkspaceId, board.Kind)
                .Columns.Single(c => c.Name == "Total").Id;

            CoreHub.KindDashboardManager.SetColumns(board.Id,
            [
                new KindDashboardColumn(totalId) { Name = "Total Renamed" }
            ]);

            var loaded = CoreHub.KindDashboardManager.GetBoard(board.WorkspaceId, board.Kind);
            var column = Assert.Single(loaded.Columns);

            Assert.Equal(totalId, column.Id);
            Assert.Equal("Total Renamed", column.Name);
            Assert.Single(column.Widgets);
        }
    }
}
