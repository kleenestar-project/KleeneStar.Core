using KleeneStar.Core.WebMaintenance;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Covers what keeps the object hierarchy sound beside the rules of a single move: the
    /// start-up <see cref="HierarchyVerifier"/>, the <see cref="HierarchyLock"/>, the
    /// <c>expectedParent</c> member the move endpoints read (<see cref="ObjectMove.ReadExpectation"/>)
    /// and the binding of <see cref="HierarchySettings"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestHierarchyMaintenance
    {
        private static readonly Guid WorkspaceId = Guid.Parse("1A2B3C4D-0001-4000-8000-00000000C001");
        private static readonly Guid ClassId = Guid.Parse("1A2B3C4D-0002-4000-8000-00000000C002");
        private static readonly Guid AlphaId = Guid.Parse("1A2B3C4D-0003-4000-8000-00000000C003");
        private static readonly Guid BetaId = Guid.Parse("1A2B3C4D-0004-4000-8000-00000000C004");
        private static readonly Guid GammaId = Guid.Parse("1A2B3C4D-0005-4000-8000-00000000C005");
        private static readonly Guid DeltaId = Guid.Parse("1A2B3C4D-0006-4000-8000-00000000C006");

        /// <summary>
        /// Seeds alpha → beta → gamma → alpha (a cycle persisted by older data, gamma changed
        /// last) and delta below alpha.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void SeedCycle(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            var day = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "hv", Name = "verifier" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Item", WorkspaceId = WorkspaceId });

            db.Objects.Add(new ObjectEntity(AlphaId) { Key = "HV-1", Summary = "alpha", WorkspaceId = WorkspaceId, ClassId = ClassId, ParentId = GammaId, Updated = day });
            db.Objects.Add(new ObjectEntity(BetaId) { Key = "HV-2", Summary = "beta", WorkspaceId = WorkspaceId, ClassId = ClassId, ParentId = AlphaId, Updated = day.AddHours(1) });
            db.Objects.Add(new ObjectEntity(GammaId) { Key = "HV-3", Summary = "gamma", WorkspaceId = WorkspaceId, ClassId = ClassId, ParentId = BetaId, Updated = day.AddHours(2) });
            db.Objects.Add(new ObjectEntity(DeltaId) { Key = "HV-4", Summary = "delta", WorkspaceId = WorkspaceId, ClassId = ClassId, ParentId = AlphaId, Updated = day.AddHours(3) });

            db.SaveChanges();
        }

        /// <summary>
        /// The cycle finder reports each cycle once with exactly its members, and nothing for
        /// chains that merely end in one or reach a root.
        /// </summary>
        [Fact]
        public void FindCycles_ReportsEachCycleWithItsMembers()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var tail = Guid.NewGuid();
            var self = Guid.NewGuid();
            var root = Guid.NewGuid();
            var child = Guid.NewGuid();

            var parents = new Dictionary<Guid, Guid?>
            {
                [tail] = a,      // hangs on the cycle, is not part of it
                [a] = b,
                [b] = c,
                [c] = a,
                [self] = self,
                [root] = null,
                [child] = root
            };

            var cycles = HierarchyVerifier.FindCycles(parents);

            Assert.Equal(2, cycles.Count);
            Assert.Contains(cycles, x => x.Count == 3 && x.ToHashSet().SetEquals([a, b, c]));
            Assert.Contains(cycles, x => x.Count == 1 && x[0] == self);
        }

        /// <summary>
        /// The verifier breaks a persisted cycle by detaching the member changed last, records
        /// the repair in its history, and leaves the rest of the tree alone.
        /// </summary>
        [Fact]
        public void Run_BreaksACycleAtTheMemberChangedLast()
        {
            SeedCycle(nameof(Run_BreaksACycleAtTheMemberChangedLast));

            var result = HierarchyVerifier.Run(null);

            Assert.Equal(1, result.Cycles);
            Assert.Equal([GammaId], result.Repaired);

            Assert.Null(CoreHub.ObjectManager.GetObject(GammaId)!.ParentId);
            Assert.Equal(GammaId, CoreHub.ObjectManager.GetObject(AlphaId)!.ParentId);
            Assert.Equal(AlphaId, CoreHub.ObjectManager.GetObject(DeltaId)!.ParentId);

            Assert.Contains(CoreHub.CommitManager.GetHistory(GammaId).SelectMany(x => x.Changes),
                x => x.Name == ObjectProperty.Parent && x.NewValue is null);

            // healthy now: a second run finds nothing to do
            var again = HierarchyVerifier.Run(null);

            Assert.Equal(0, again.Cycles);
            Assert.Empty(again.Repaired);
        }

        /// <summary>
        /// Objects deeper than the limit are reported, not moved.
        /// </summary>
        [Fact]
        public void Run_ReportsTooDeepObjectsWithoutMovingThem()
        {
            SeedCycle(nameof(Run_ReportsTooDeepObjectsWithoutMovingThem));

            // after the repair: gamma (1) → alpha (2) → beta (3), delta (3)
            UnitTestObjectManagerHierarchy.WithMaxDepth(2, () =>
            {
                var result = HierarchyVerifier.Run(null);

                Assert.Equal(2, result.TooDeep);
                Assert.Equal(AlphaId, CoreHub.ObjectManager.GetObject(BetaId)!.ParentId);
            });
        }

        /// <summary>
        /// The lock is re-entrant on the holding thread and keeps every other thread out until
        /// the outermost handle is released.
        /// </summary>
        /// <remarks>
        /// The lock is a monitor: a handle has to be released on the thread that took it, so the
        /// test thread takes and releases its handles without an <c>await</c> in between, and the
        /// hold of the last part is taken on a task of its own.
        /// </remarks>
        [Fact]
        public async Task Lock_IsReentrantAndExclusive()
        {
            var workspace = Guid.NewGuid();
            var entered = new ManualResetEventSlim();
            var cancellation = TestContext.Current.CancellationToken;

            var outer = HierarchyLock.Enter(workspace);
            var inner = HierarchyLock.Enter(workspace);

            inner.Dispose();

            var other = Task.Run(() =>
            {
                using (HierarchyLock.Enter(workspace))
                {
                    entered.Set();
                }
            }, cancellation);

            Assert.False(entered.Wait(TimeSpan.FromMilliseconds(300), cancellation), "another thread entered while the lock was held");

            outer.Dispose();

            Assert.True(entered.Wait(TimeSpan.FromSeconds(10), cancellation), "the lock was not released by the outer handle");
            await other.WaitAsync(TimeSpan.FromSeconds(10), cancellation);

            // another workspace never waits while this one is held
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            var holder = Task.Run(() =>
            {
                using (HierarchyLock.Enter(workspace))
                {
                    held.Set();
                    release.Wait(cancellation);
                }
            }, cancellation);

            Assert.True(held.Wait(TimeSpan.FromSeconds(10), cancellation), "the holder did not take the lock");

            try
            {
                await Task.Run(() => { using (HierarchyLock.Enter(Guid.NewGuid())) { } }, cancellation)
                    .WaitAsync(TimeSpan.FromSeconds(10), cancellation);
            }
            finally
            {
                release.Set();
            }

            await holder.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
        }

        /// <summary>
        /// <c>expectedParent</c> has three states - absent, <c>null</c>, a key - and a key that
        /// resolves to nothing reads as an expectation that cannot hold.
        /// </summary>
        [Fact]
        public void ReadExpectation_DistinguishesAbsentNullAndKey()
        {
            SeedCycle(nameof(ReadExpectation_DistinguishesAbsentNullAndKey));

            static JsonElement Member(string json)
            {
                using var document = JsonDocument.Parse(json);

                return document.RootElement.TryGetProperty("expectedParent", out var element)
                    ? element.Clone()
                    : default;
            }

            Assert.False(ObjectMove.ReadExpectation(Member("{ \"node\": \"HV-4\" }"), out _));

            Assert.True(ObjectMove.ReadExpectation(Member("{ \"expectedParent\": null }"), out var root));
            Assert.Null(root);

            Assert.True(ObjectMove.ReadExpectation(Member("{ \"expectedParent\": \"HV-1\" }"), out var alpha));
            Assert.Equal(AlphaId, alpha);

            Assert.True(ObjectMove.ReadExpectation(Member("{ \"expectedParent\": \"HV-404\" }"), out var unknown));
            Assert.Equal(Guid.Empty, unknown);
        }

        /// <summary>
        /// A JSON <c>null</c> reaches a <see cref="JsonElement"/> payload member as
        /// <see cref="JsonValueKind.Null"/> and an absent member stays undefined - the
        /// distinction the move endpoints rely on.
        /// </summary>
        [Fact]
        public void PayloadMember_KeepsNullApartFromAbsent()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var absent = JsonSerializer.Deserialize<Payload>("{ \"node\": \"X\" }", options)!;
            var explicitNull = JsonSerializer.Deserialize<Payload>("{ \"node\": \"X\", \"expectedParent\": null }", options)!;

            Assert.Equal(JsonValueKind.Undefined, absent.ExpectedParent.ValueKind);
            Assert.Equal(JsonValueKind.Null, explicitNull.ExpectedParent.ValueKind);
        }

        /// <summary>
        /// The settings bind from the plugin's <c>Hierarchy</c> section and keep their defaults
        /// without one; a nonsensical limit still permits one level.
        /// </summary>
        [Fact]
        public void Settings_BindFromThePluginSection()
        {
            var configured = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["Hierarchy:MaxDepth"] = "7" })
                .Build();

            var zero = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { ["Hierarchy:MaxDepth"] = "0" })
                .Build();

            Assert.Equal(7, HierarchySettings.From(configured).MaxDepth);
            Assert.Equal(32, HierarchySettings.From(null).MaxDepth);
            Assert.Equal(32, HierarchySettings.From(new ConfigurationBuilder().Build()).MaxDepth);
            Assert.Equal(1, HierarchySettings.From(zero).EffectiveMaxDepth);
        }

        /// <summary>
        /// The shape of the move endpoints' bodies, for the deserialization test.
        /// </summary>
        private sealed class Payload
        {
            public string Node { get; set; }

            public JsonElement ExpectedParent { get; set; }
        }
    }
}
