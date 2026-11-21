using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Tests what the start page of a class reports (<see cref="ClassOverview"/>): the key
    /// figures and their windows, the weekly history, the recently changed objects and the setup
    /// checks of a class nothing was set up for.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassOverview
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C1A5E000-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("C1A5E000-0000-4000-8000-000000000002");
        private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Seeds an issue class with no structure at all and three objects of different ages.
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

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "CO", Name = "overview" });
            db.Classes.Add(new Model.Entities.Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });

            // one brand new, one of last month changed today, one old and untouched
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CO-1", Summary = "new", WorkspaceId = WorkspaceId, ClassId = ClassId, Created = Now.AddDays(-1), Updated = Now.AddDays(-1) });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CO-2", Summary = "touched", WorkspaceId = WorkspaceId, ClassId = ClassId, Created = Now.AddDays(-40), Updated = Now.AddHours(-2) });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CO-3", Summary = "old", WorkspaceId = WorkspaceId, ClassId = ClassId, Created = Now.AddDays(-200), Updated = Now.AddDays(-150) });
            db.SaveChanges();
        }

        /// <summary>
        /// The key figures count within their windows, a class without a workflow reports no open
        /// count and no status distribution, the history has twelve weeks with the new object in
        /// the last one, and the recently changed objects come newest first.
        /// </summary>
        [Fact]
        public void TheFiguresCountWithinTheirWindows()
        {
            Seed(nameof(TheFiguresCountWithinTheirWindows));

            var overview = ClassOverview.Build(CoreHub.ClassManager.GetClass(ClassId), Now);

            Assert.Equal(3, overview.Total);
            Assert.Equal(1, overview.CreatedRecently);
            Assert.Equal(2, overview.UpdatedRecently);
            Assert.Null(overview.Open);
            Assert.Empty(overview.Categories);
            Assert.Equal(ClassOverview.Weeks, overview.WeeklyCreated.Count);
            // twelve weeks reach back 84 days: the new object and the one of 40 days ago count,
            // the one of 200 days ago does not
            Assert.Equal(1, overview.WeeklyCreated[^1]);
            Assert.Equal(2, overview.WeeklyCreated.Sum());
            Assert.Equal(["CO-2", "CO-1", "CO-3"], overview.Recent.Select(x => x.Object.Key));
        }

        /// <summary>
        /// A work-item class nothing was set up for says so - no workflow, no fields, no
        /// permissions - and those warnings come before everything else.
        /// </summary>
        [Fact]
        public void AnEmptyWorkItemClassIsReportedAsIncomplete()
        {
            Seed(nameof(AnEmptyWorkItemClassIsReportedAsIncomplete));

            var checks = ClassOverview.Build(CoreHub.ClassManager.GetClass(ClassId), Now).Checks;
            var keys = checks.Select(x => x.Key).ToList();

            Assert.Contains("workflow.none", keys);
            Assert.Contains("fields.none", keys);
            Assert.Contains("permissions.none", keys);
            Assert.Contains("templates.none", keys);
            Assert.Contains("priorities.none", keys);

            var firstNonWarning = checks.ToList().FindIndex(x => x.State != SetupCheckState.Warning);
            Assert.True(checks.Take(firstNonWarning).All(x => x.State == SetupCheckState.Warning));
            Assert.All(checks.Skip(firstNonWarning), x => Assert.NotEqual(SetupCheckState.Warning, x.State));
        }
    }
}
