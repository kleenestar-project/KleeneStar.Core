using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System.Diagnostics;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.Load
{
    /// <summary>
    /// Measures the object hierarchy at its target sizes (<c>kleenestar.hierarchy.md</c>):
    /// 100,000 objects, 10,000 sections about thirteen levels deep, one section holding 10,000
    /// pages - and fails when a step leaves its budget: rebuilding the path index, counting a
    /// subtree, reading the ancestors of a deep object, moving the big section (one path rewrite
    /// of its whole subtree).
    /// </summary>
    /// <remarks>
    /// Opt-in (<c>KLEENESTAR_LOAD=1</c>) like <see cref="UnitTestLoad"/>, on a real SQLite file with
    /// every migration applied - the in-memory provider has no indexes.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestHierarchyLoad(ITestOutputHelper output)
    {
        private const int Sections = 10_000;
        private const int Objects = 90_000;
        private const int BigSection = 10_000;
        private static readonly Guid WorkspaceId = Guid.Parse("10AD0000-0000-4000-8000-0000000C0001");
        private static readonly Guid SectionClassId = Guid.Parse("10AD0000-0000-4000-8000-0000000C0002");
        private static readonly Guid DocClassId = Guid.Parse("10AD0000-0000-4000-8000-0000000C0003");

        private void Measure(string name, TimeSpan budget, Action step)
        {
            var watch = Stopwatch.StartNew();

            step();
            watch.Stop();

            output.WriteLine($"{name}: {watch.Elapsed.TotalMilliseconds:0} ms (budget {budget.TotalMilliseconds:0} ms)");
            Assert.True(watch.Elapsed < budget, $"{name} took {watch.Elapsed.TotalMilliseconds:0} ms, more than {budget.TotalMilliseconds:0} ms.");
        }

        /// <summary>
        /// The hierarchy at its target sizes.
        /// </summary>
        [Fact]
        public void HierarchyAtTargetSize_StaysWithinBudget()
        {
            Assert.SkipUnless(Environment.GetEnvironmentVariable("KLEENESTAR_LOAD") == "1", "Set KLEENESTAR_LOAD=1 to run the load test.");

            var database = nameof(HierarchyAtTargetSize_StaysWithinBudget);
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
                ObjectPathIndex.Disable();
                ModelHub.DatabaseSettings = previous;
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            }
        }

        private void Run()
        {
            using (var schema = ModelHub.CreateDbContext())
            {
                Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(schema.Database);
            }

            var sections = new Guid[Sections];
            var big = Guid.NewGuid();
            var target = Guid.Empty;

            Measure("seed 10,000 sections and 100,000 objects", TimeSpan.FromSeconds(240), () =>
            {
                using var db = ModelHub.CreateDbContext();

                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "XLD", Name = "Hierarchy load" });
                db.Classes.Add(new Class { Id = SectionClassId, Name = "Section", Kind = ObjectKind.Document, WorkspaceId = WorkspaceId });
                db.Classes.Add(new Class { Id = DocClassId, Name = "Page", Kind = ObjectKind.Document, WorkspaceId = WorkspaceId });

                var key = 0;

                // a binary tree of sections: about thirteen levels for ten thousand
                for (var i = 0; i < Sections; i++)
                {
                    sections[i] = Guid.NewGuid();
                    db.Objects.Add(new ObjectEntity(sections[i])
                    {
                        Key = "XLD-" + ++key,
                        Summary = "section " + i,
                        Kind = ObjectKind.Document,
                        WorkspaceId = WorkspaceId,
                        ClassId = SectionClassId,
                        ParentId = i == 0 ? null : sections[(i - 1) / 2]
                    });
                }

                for (var j = 0; j < Objects - BigSection; j++)
                {
                    db.Objects.Add(new ObjectEntity(Guid.NewGuid())
                    {
                        Key = "XLD-" + ++key,
                        Summary = "page " + j,
                        Kind = ObjectKind.Document,
                        WorkspaceId = WorkspaceId,
                        ClassId = DocClassId,
                        ParentId = sections[j % Sections]
                    });
                }

                // the section of ten thousand, below a shallow section
                db.Objects.Add(new ObjectEntity(big)
                {
                    Key = "XLD-" + ++key,
                    Summary = "big",
                    Kind = ObjectKind.Document,
                    WorkspaceId = WorkspaceId,
                    ClassId = SectionClassId,
                    ParentId = sections[1]
                });

                for (var j = 0; j < BigSection; j++)
                {
                    db.Objects.Add(new ObjectEntity(Guid.NewGuid())
                    {
                        Key = "XLD-" + ++key,
                        Summary = "bulk " + j,
                        Kind = ObjectKind.Document,
                        WorkspaceId = WorkspaceId,
                        ClassId = DocClassId,
                        ParentId = big
                    });
                }

                db.ChangeTracker.DetectChanges();
                db.SaveChanges();
            });

            Measure("rebuild the path index", TimeSpan.FromSeconds(90), () => ObjectPathIndex.Rebuild());
            Assert.True(ObjectPathIndex.Ready);

            var middle = sections[Sections / 3];

            // warm the query plans once; the budgets are for a running installation
            ObjectHierarchy.GetDescendantIds(middle);

            Measure("subtree ids of a middle section", TimeSpan.FromMilliseconds(500), () =>
                Assert.NotEmpty(ObjectHierarchy.GetDescendantIds(middle)));

            Measure("count below the root section", TimeSpan.FromMilliseconds(500), () =>
                Assert.Equal(Sections - 1 + Objects + 1, ObjectPathIndex.Count(sections[0])));

            Measure("ancestors of a deep section", TimeSpan.FromMilliseconds(100), () =>
                Assert.True(ObjectPathIndex.Ancestors(sections[Sections - 1])!.Count >= 12));

            target = sections[Sections - 1];

            Measure("move the section of 10,000 below the deepest section", TimeSpan.FromSeconds(1), () =>
                CoreHub.ObjectManager.SetParent(big, target));

            var sample = ModelHub.GetSubtree(WorkspaceId, ModelHub.GetObjectPath(big)!.Path + ModelHub.PathSegment(big), 1).Single();
            var depth = ModelHub.GetObjectPath(target)!.Depth + 2;

            Assert.Equal(depth, sample.Depth);
            Assert.Equal(BigSection, ObjectPathIndex.Count(big));
        }
    }
}
