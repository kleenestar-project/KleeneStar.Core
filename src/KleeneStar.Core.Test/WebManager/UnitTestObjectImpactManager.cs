using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectImpactManager"/> —
    /// the transitive walk that answers what changing an object touches.
    /// </summary>
    /// <remarks>
    /// The relations are defined by the tests rather than assumed to exist, which is the point
    /// of the model: nothing in the code knows a relation by name, only what its type declares
    /// it does.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestObjectImpactManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("2A6F1C40-0E5B-4F2A-9E3D-7C1B8A5D4E01");
        private static readonly Guid ClassId = Guid.Parse("2A6F1C40-0E5B-4F2A-9E3D-7C1B8A5D4E02");

        /// <summary>
        /// Seeds a workspace with one class and publishes the three relations whose effects the
        /// walk follows, plus one that carries none.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-impact", Name = "impact" });
                db.Classes.Add(new Class { Id = ClassId, Name = "Task", WorkspaceId = WorkspaceId });
                db.SaveChanges();
            }

            PublishRelations();
        }

        /// <summary>
        /// Publishes one relation per effect, so each direction can be told apart, and one
        /// informational relation that must not be followed at all.
        /// </summary>
        private static void PublishRelations()
        {
            void publish(string key, RelationEffect effect, int order) => CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = key,
                Label = key,
                InverseLabel = "inverse " + key,
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Effect = effect,
                Active = true,
                Order = order
            });

            publish("blocks", RelationEffect.BlocksCompletion, 1);
            publish("duplicate", RelationEffect.ClosesItem, 2);
            publish("parent", RelationEffect.AggregatesProgress, 3);
            publish("references", RelationEffect.None, 4);
        }

        /// <summary>
        /// Creates an object of the seeded class.
        /// </summary>
        /// <param name="key">The object key, which is also its summary.</param>
        /// <returns>The id of the created object.</returns>
        private static Guid Object(string key)
        {
            var id = Guid.NewGuid();

            CoreHub.ObjectManager.Add(new ObjectEntity
            {
                Id = id,
                Key = key,
                Summary = key,
                WorkspaceId = WorkspaceId,
                ClassId = ClassId
            });

            return id;
        }

        /// <summary>
        /// Stores one relation of the supplied type from one object to another.
        /// </summary>
        /// <param name="typeKey">The relation type.</param>
        /// <param name="sourceId">The end the relation is stored from.</param>
        /// <param name="targetId">The end it points at.</param>
        /// <param name="status">The lifecycle state of the relation.</param>
        private static void Relate(string typeKey, Guid sourceId, Guid targetId, RelationStatus status = RelationStatus.Active)
        {
            CoreHub.ObjectRelationManager.Add(new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = typeKey,
                Direction = RelationDirection.Bidirectional,
                Status = status,
                SourceObjectId = sourceId,
                TargetObjectId = targetId
            });
        }

        /// <summary>
        /// Verifies the chain the whole feature exists for: a blocker two steps away from what it
        /// finally holds up is reported, with the distance saying how far away it stands.
        /// </summary>
        /// <remarks>
        /// The one-hop reading of the same data — the relation surface of the origin — knows only
        /// the first of the two, which is exactly the gap the analysis closes.
        /// </remarks>
        [Fact]
        public void Analyze_FollowsAChainOfBlockers()
        {
            Seed(nameof(Analyze_FollowsAChainOfBlockers));

            var a = Object("IMP-A");
            var b = Object("IMP-B");
            var c = Object("IMP-C");

            // A blocks B, B blocks C: finishing C waits on both
            Relate("blocks", a, b);
            Relate("blocks", b, c);

            var result = CoreHub.ObjectImpactManager.Analyze(a);

            Assert.Equal(3, result.Nodes.Count);
            Assert.Equal(0, result.Nodes.Single(x => x.Object.Id == a).Distance);
            Assert.Equal(1, result.Nodes.Single(x => x.Object.Id == b).Distance);
            Assert.Equal(2, result.Nodes.Single(x => x.Object.Id == c).Distance);
            Assert.Equal(2, result.Edges.Count);
            Assert.False(result.Truncated);
        }

        /// <summary>
        /// Verifies that the walk runs the way the consequence runs, which is not the same as the
        /// way the relation is stored: a blocker reaches what it blocks, while the object it
        /// blocks reaches nothing through that relation.
        /// </summary>
        [Fact]
        public void Analyze_FollowsBlocksFromTheBlockerOnly()
        {
            Seed(nameof(Analyze_FollowsBlocksFromTheBlockerOnly));

            var blocker = Object("IMP-BLOCKER");
            var blocked = Object("IMP-BLOCKED");

            Relate("blocks", blocker, blocked);

            var downstream = CoreHub.ObjectImpactManager.Analyze(blocker);
            var upstream = CoreHub.ObjectImpactManager.Analyze(blocked);

            Assert.Contains(downstream.Nodes, x => x.Object.Id == blocked);
            Assert.Single(upstream.Nodes);
            Assert.Empty(upstream.Edges);
        }

        /// <summary>
        /// Verifies the two effects that run the other way: the duplicate follows its original
        /// and the parent reports its child, so closing or moving the <em>target</em> is what
        /// touches the source.
        /// </summary>
        [Fact]
        public void Analyze_FollowsClosingAndAggregatingTowardsTheSource()
        {
            Seed(nameof(Analyze_FollowsClosingAndAggregatingTowardsTheSource));

            var original = Object("IMP-ORIGINAL");
            var duplicate = Object("IMP-DUPLICATE");
            var child = Object("IMP-CHILD");
            var parent = Object("IMP-PARENT");

            // "duplicate of" and "parent of" are stored from the follower and from the parent
            Relate("duplicate", duplicate, original);
            Relate("parent", parent, child);

            var fromOriginal = CoreHub.ObjectImpactManager.Analyze(original);
            var fromChild = CoreHub.ObjectImpactManager.Analyze(child);
            var fromDuplicate = CoreHub.ObjectImpactManager.Analyze(duplicate);

            Assert.Contains(fromOriginal.Nodes, x => x.Object.Id == duplicate);
            Assert.Contains(fromChild.Nodes, x => x.Object.Id == parent);

            // ...and not back: settling the duplicate does not settle the original
            Assert.Single(fromDuplicate.Nodes);
        }

        /// <summary>
        /// Verifies that an informational relation carries nothing. It states that two records
        /// have something to do with each other, not that one governs the other, and following it
        /// would answer what the object is connected to — which the relation surface already
        /// answers.
        /// </summary>
        [Fact]
        public void Analyze_IgnoresRelationsWithoutAnEffect()
        {
            Seed(nameof(Analyze_IgnoresRelationsWithoutAnEffect));

            var origin = Object("IMP-REF-A");
            var other = Object("IMP-REF-B");

            Relate("references", origin, other);

            var result = CoreHub.ObjectImpactManager.Analyze(origin);

            Assert.Single(result.Nodes);
            Assert.Empty(result.Edges);
        }

        /// <summary>
        /// Verifies that an obsolete relation carries nothing either: it is kept for the history,
        /// and history does not propagate a change made today — the same reading the workflow
        /// guard gives it.
        /// </summary>
        [Fact]
        public void Analyze_IgnoresObsoleteRelations()
        {
            Seed(nameof(Analyze_IgnoresObsoleteRelations));

            var origin = Object("IMP-OBS-A");
            var other = Object("IMP-OBS-B");

            Relate("blocks", origin, other, RelationStatus.Obsolete);

            var result = CoreHub.ObjectImpactManager.Analyze(origin);

            Assert.Single(result.Nodes);
        }

        /// <summary>
        /// Verifies that the depth is a limit on the walk rather than on the answer: at one step
        /// the second link is not reported, at two it is.
        /// </summary>
        [Fact]
        public void Analyze_StopsAtTheRequestedDepth()
        {
            Seed(nameof(Analyze_StopsAtTheRequestedDepth));

            var a = Object("IMP-D-A");
            var b = Object("IMP-D-B");
            var c = Object("IMP-D-C");

            Relate("blocks", a, b);
            Relate("blocks", b, c);

            var shallow = CoreHub.ObjectImpactManager.Analyze(a, 1);
            var deeper = CoreHub.ObjectImpactManager.Analyze(a, 2);

            Assert.Equal(2, shallow.Nodes.Count);
            Assert.DoesNotContain(shallow.Nodes, x => x.Object.Id == c);
            Assert.Equal(3, deeper.Nodes.Count);
        }

        /// <summary>
        /// Verifies that a cycle ends the walk instead of running forever, and that the edge
        /// closing it is still reported — a cycle drawn as a chain would be a different graph.
        /// </summary>
        /// <remarks>
        /// Two objects can be each other's blocker, which is a mistake somebody made rather than
        /// a case to refuse; the analysis has to survive it and show it.
        /// </remarks>
        [Fact]
        public void Analyze_SurvivesACycleAndKeepsItsEdge()
        {
            Seed(nameof(Analyze_SurvivesACycleAndKeepsItsEdge));

            var a = Object("IMP-C-A");
            var b = Object("IMP-C-B");

            Relate("blocks", a, b);
            Relate("blocks", b, a);

            var result = CoreHub.ObjectImpactManager.Analyze(a, 5);

            Assert.Equal(2, result.Nodes.Count);
            Assert.Equal(2, result.Edges.Count);
            Assert.Contains(result.Edges, x => x.From == a && x.To == b);
            Assert.Contains(result.Edges, x => x.From == b && x.To == a);
        }

        /// <summary>
        /// Verifies that an object nobody can address answers an empty analysis rather than an
        /// error — the same reading a list of objects gives a key that names nothing.
        /// </summary>
        [Fact]
        public void Analyze_UnknownObject_IsEmpty()
        {
            Seed(nameof(Analyze_UnknownObject_IsEmpty));

            var result = CoreHub.ObjectImpactManager.Analyze(Guid.NewGuid());

            Assert.Null(result.Origin);
            Assert.Empty(result.Nodes);
            Assert.Empty(result.Edges);
        }
    }
}
