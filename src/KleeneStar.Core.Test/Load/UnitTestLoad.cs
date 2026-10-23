using KleeneStar.Core.WebAutomation;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System.Diagnostics;
using Export = KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Export;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.Load
{
    /// <summary>
    /// Measures the paths that grow with the data at the volume of a busy service desk - 60,000
    /// tickets, a year's worth - and fails when one of them leaves its budget: the scheduler
    /// walking every object of a rule's scope, the queue working off a burst of events, the
    /// team's load, the export of everything.
    /// </summary>
    /// <remarks>
    /// Opt-in (<c>KLEENESTAR_LOAD=1</c>), because it takes its time. It runs on a real SQLite
    /// file with every migration applied: the in-memory provider of the other tests has no
    /// indexes and would measure its own table scans rather than the application.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestLoad(ITestOutputHelper output)
    {
        private const int Tickets = 60_000;
        private static readonly Guid WorkspaceId = Guid.Parse("10AD0000-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("10AD0000-0000-4000-8000-000000000002");
        private static readonly Guid AgentId = Guid.Parse("10AD0000-0000-4000-8000-000000000003");

        /// <summary>
        /// Runs a step and reports its time.
        /// </summary>
        /// <param name="name">The step.</param>
        /// <param name="budget">The time it may take.</param>
        /// <param name="step">The step.</param>
        private void Measure(string name, TimeSpan budget, Action step)
        {
            var watch = Stopwatch.StartNew();

            step();
            watch.Stop();

            output.WriteLine($"{name}: {watch.Elapsed.TotalSeconds:0.00} s (budget {budget.TotalSeconds:0} s)");
            Assert.True(watch.Elapsed < budget, $"{name} took {watch.Elapsed.TotalSeconds:0.0} s, more than {budget.TotalSeconds:0} s.");
        }

        /// <summary>
        /// A year of tickets.
        /// </summary>
        [Fact]
        public void AYearOfTickets_StaysWithinBudget()
        {
            Assert.SkipUnless(Environment.GetEnvironmentVariable("KLEENESTAR_LOAD") == "1", "Set KLEENESTAR_LOAD=1 to run the load test.");

            var database = nameof(AYearOfTickets_StaysWithinBudget);
            CoreHubFixture.Initialize(database);

            var directory = Path.Combine(Path.GetTempPath(), "kleenestar-load-" + Guid.NewGuid().ToString("N"));
            var previous = ModelHub.DatabaseSettings;

            Directory.CreateDirectory(directory);
            ModelHub.DatabaseSettings = new Model.Settings.DatabaseSettings
            {
                Assembly = "KleeneStar.Model.Sqlite",
                ConnectionString = $"Data Source={Path.Combine(directory, "load.db")}"
            };

            try
            {
                Run();
            }
            finally
            {
                ModelHub.DatabaseSettings = previous;

                // the pooled connections hold the file until they are let go
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            }
        }

        /// <summary>
        /// The steps of the load test.
        /// </summary>
        private void Run()
        {
            using (var schema = ModelHub.CreateDbContext())
            {
                Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(schema.Database);
            }

            var assignees = Enumerable.Range(0, 200).Select(i => Guid.NewGuid()).ToList();
            var ids = new List<Guid>(Tickets);

            Measure("seed 60,000 tickets", TimeSpan.FromSeconds(120), () =>
            {
                using var db = ModelHub.CreateDbContext();

                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "LOAD", Name = "Load" });
                db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
                db.Identities.Add(new Identity { Id = AgentId, Name = "Agent", UserName = "agent", Email = "agent@example.com", PasswordHash = "x" });

                foreach (var (assignee, index) in assignees.Select((x, i) => (x, i)))
                {
                    db.Identities.Add(new Identity { Id = assignee, Name = "Person " + index, UserName = "person" + index, Email = $"person{index}@example.com", PasswordHash = "x" });
                }

                var start = DateTime.UtcNow.AddDays(-365);

                for (var index = 0; index < Tickets; index++)
                {
                    var id = Guid.NewGuid();

                    ids.Add(id);
                    db.Objects.Add(new ObjectEntity(id)
                    {
                        Key = "LOAD-" + (index + 1),
                        Summary = index % 1000 == 0 ? "special case " + index : "ticket " + index,
                        WorkspaceId = WorkspaceId,
                        ClassId = ClassId,
                        AssigneeId = assignees[index % assignees.Count],
                        Created = start.AddMinutes(index * 8.76),
                        Updated = start.AddMinutes(index * 8.76)
                    });
                }

                db.SaveChanges();
            });

            CoreHub.AutomationManager.AddRule(new AutomationRule
            {
                Name = "every special ticket",
                WorkspaceId = WorkspaceId,
                Trigger = AutomationTriggerCatalog.Schedule,
                TriggerParameter = "60",
                Condition = "Summary ~ \"special\"",
                Actions = "comment internal: checked",
                RunAsId = AgentId,
                State = AutomationRuleState.Active
            });

            var creation = new AutomationRule
            {
                Name = "on creation",
                WorkspaceId = WorkspaceId,
                Trigger = AutomationTriggerCatalog.ObjectCreated,
                Actions = "comment internal: welcome",
                RunAsId = AgentId,
                State = AutomationRuleState.Active
            };

            CoreHub.AutomationManager.AddRule(creation);

            var queued = 0;

            Measure("scheduler over 60,000 objects", TimeSpan.FromSeconds(60), () => queued = CoreHub.AutomationManager.RunSchedules(DateTime.UtcNow));
            Assert.Equal(Tickets / 1000, queued);

            Measure("raise 2,000 events", TimeSpan.FromSeconds(60), () =>
            {
                for (var index = 0; index < 2000; index++)
                {
                    CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = ids[index], Occurrence = "load:" + index });
                }
            });

            Measure("work off the queue", TimeSpan.FromSeconds(180), () =>
            {
                while (CoreHub.AutomationManager.ProcessDue(100) > 0)
                {
                }
            });

            Assert.Equal(2000, ModelHub.GetAutomationJobs([creation.Id], AutomationJobState.Succeeded, 5000).Count);

            Measure("team load of 200 people", TimeSpan.FromSeconds(60), () => Assert.Equal(200, CoreHub.PlanningManager.GetTeamLoad(WorkspaceId, DateTime.UtcNow).Count));

            Measure("export 60,000 rows", TimeSpan.FromSeconds(60), () =>
            {
                var objects = CoreHub.ObjectManager.GetObjects(new WebExpress.WebIndex.Queries.Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId)).ToList();

                Assert.Equal(Tickets, objects.Count);
                Assert.True(Export.Write(objects, ["key", "summary", "workspace", "class", "assignee", "created"], ';', DateTime.UtcNow).ToString().Length > Tickets * 20);
            });
        }
    }
}
