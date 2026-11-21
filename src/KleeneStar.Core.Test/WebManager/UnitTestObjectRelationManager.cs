using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectRelationManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectRelationManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("88CCDDEE-FF00-1122-3344-55667788AABB");
        private static readonly Guid ClassId = Guid.Parse("99DDEEFF-0011-2233-4455-66778899BBCC");
        private static readonly Guid SourceId = Guid.Parse("AAEEFF00-1122-3344-5566-778899AABBCC");
        private static readonly Guid TargetId = Guid.Parse("BBFF0011-2233-4455-6677-8899AABBCCDD");

        /// <summary>
        /// The key of the relation the tests use. It is an arbitrary string rather than a
        /// constant of the model on purpose: relations are defined by whoever runs the
        /// installation, so nothing in the code may depend on a particular one existing.
        /// </summary>
        private const string BlocksKey = "blocks";

        /// <summary>
        /// Seeds the in-memory database with two objects between which relations are tested.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-relation", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Objects.Any(x => x.Id == SourceId))
            {
                db.Objects.Add(new ObjectEntity { Id = SourceId, Key = "SRC-1", Summary = "source", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }
            if (!db.Objects.Any(x => x.Id == TargetId))
            {
                db.Objects.Add(new ObjectEntity { Id = TargetId, Key = "TGT-1", Summary = "target", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the relation and that <c>GetRelations</c>
        /// returns it from either side of it - one stored row, two readings.
        /// </summary>
        [Fact]
        public void Add_Then_GetRelations_RoundTrip()
        {
            Seed(nameof(Add_Then_GetRelations_RoundTrip));

            var relation = Sample(BlocksKey);
            CoreHub.ObjectRelationManager.Add(relation);

            var fromSource = CoreHub.ObjectRelationManager.GetRelations(SourceId).ToList();
            var fromTarget = CoreHub.ObjectRelationManager.GetRelations(TargetId).ToList();

            Assert.Single(fromSource);
            Assert.Single(fromTarget);
            Assert.Equal(relation.Id, fromSource[0].Id);
            Assert.Equal(relation.Id, fromTarget[0].Id);
        }

        /// <summary>
        /// Verifies that a relation pointing at an address outside the installation is stored
        /// without a target object, which is the one structural difference between the two
        /// categories of the hybrid model.
        /// </summary>
        [Fact]
        public void Add_External_StoresAddressWithoutTargetObject()
        {
            Seed(nameof(Add_External_StoresAddressWithoutTargetObject));

            var relation = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Web,
                TypeKey = "weblink",
                Direction = RelationDirection.Unidirectional,
                SourceObjectId = SourceId,
                TargetUri = "https://example.com/advisory",
                TargetTitle = "Vendor advisory"
            };

            CoreHub.ObjectRelationManager.Add(relation);

            var stored = CoreHub.ObjectRelationManager.GetRelations(SourceId).Single();

            Assert.Null(stored.TargetObjectId);
            Assert.Equal("https://example.com/advisory", stored.TargetUri);
            Assert.Equal("Vendor advisory", stored.TargetTitle);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes the changeable fields back and raises the
        /// <see cref="KleeneStar.Core.WebManager.IObjectRelationManager.RelationUpdated"/>
        /// event, while leaving the two ends where they are.
        /// </summary>
        [Fact]
        public void Update_ChangesLifecycleAndRaisesEvent()
        {
            Seed(nameof(Update_ChangesLifecycleAndRaisesEvent));

            var relation = Sample(BlocksKey);
            CoreHub.ObjectRelationManager.Add(relation);

            ObjectRelation raised = null;
            CoreHub.ObjectRelationManager.RelationUpdated += (_, x) => raised = x;

            relation.Status = RelationStatus.Obsolete;
            relation.Comment = "the change landed";

            CoreHub.ObjectRelationManager.Update(relation);

            var stored = CoreHub.ObjectRelationManager.GetRelation(relation.Id);

            Assert.NotNull(raised);
            Assert.Equal(RelationStatus.Obsolete, stored.Status);
            Assert.Equal("the change landed", stored.Comment);
            Assert.Equal(SourceId, stored.SourceObjectId);
            Assert.Equal(TargetId, stored.TargetObjectId);
        }

        /// <summary>
        /// Verifies that <c>GetUsage</c> counts the stored relations of one type, which is the
        /// number the type administration reports and its delete guards against.
        /// </summary>
        [Fact]
        public void GetUsage_CountsStoredRelationsOfType()
        {
            Seed(nameof(GetUsage_CountsStoredRelationsOfType));

            CoreHub.ObjectRelationManager.Add(Sample(BlocksKey));

            Assert.Equal(1, CoreHub.ObjectRelationManager.GetUsage(BlocksKey));
            Assert.Equal(0, CoreHub.ObjectRelationManager.GetUsage("causes"));
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the relation and raises the
        /// <see cref="KleeneStar.Core.WebManager.IObjectRelationManager.RelationRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var relation = Sample("references");
            CoreHub.ObjectRelationManager.Add(relation);

            ObjectRelation raised = null;
            CoreHub.ObjectRelationManager.RelationRemoved += (_, x) => raised = x;

            CoreHub.ObjectRelationManager.Remove(relation);

            Assert.Empty(CoreHub.ObjectRelationManager.GetRelations(SourceId));
            Assert.NotNull(raised);
            Assert.Equal(relation.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetRelations</c> returns an empty collection when the object takes
        /// part in none.
        /// </summary>
        [Fact]
        public void GetRelations_Unrelated_ReturnsEmpty()
        {
            Seed(nameof(GetRelations_Unrelated_ReturnsEmpty));

            Assert.Empty(CoreHub.ObjectRelationManager.GetRelations(SourceId));
        }

        /// <summary>
        /// Creates a sample <see cref="ObjectRelation"/> from the seeded source to the seeded
        /// target object.
        /// </summary>
        /// <param name="typeKey">The key of the relation the sample carries.</param>
        /// <returns>The sample relation.</returns>
        private static ObjectRelation Sample(string typeKey) => new()
        {
            Id = Guid.NewGuid(),
            System = RelationSystem.Object,
            TypeKey = typeKey,
            SourceObjectId = SourceId,
            TargetObjectId = TargetId
        };
    }
}
