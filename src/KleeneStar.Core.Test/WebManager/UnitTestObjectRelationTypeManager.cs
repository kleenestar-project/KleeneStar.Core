using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for
    /// <see cref="KleeneStar.Core.WebManager.ObjectRelationTypeManager"/>, whose whole point is
    /// that the stored table - not any fixed set in code - decides which relations exist.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectRelationTypeManager
    {
        /// <summary>
        /// Prepares an isolated database for a test.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);
        }

        /// <summary>
        /// Verifies that a relation an administrator defines becomes available to every surface
        /// that reads the registry, without a restart and without a code change.
        /// </summary>
        [Fact]
        public void Store_PublishesTypeIntoRegistry()
        {
            Seed(nameof(Store_PublishesTypeIntoRegistry));

            CoreHub.ObjectRelationTypeManager.Store(Sample("mitigates", "mitigates", "is mitigated by"));

            var published = RelationRegistry.GetType("mitigates");

            Assert.NotNull(published);
            Assert.Equal("mitigates", published.Label);
            Assert.Equal("is mitigated by", published.InverseLabel);
        }

        /// <summary>
        /// Verifies that the stored table is the whole catalog: publishing does not leave the
        /// relations WebExpress registers by default standing beside it, because an
        /// administrator who deleted one would otherwise see it return on the next start.
        /// </summary>
        [Fact]
        public void Publish_ReplacesFrameworkDefaults()
        {
            Seed(nameof(Publish_ReplacesFrameworkDefaults));

            CoreHub.ObjectRelationTypeManager.Store(Sample("supersedes", "supersedes", "is superseded by"));

            var published = RelationRegistry.Types.Select(x => x.Id).ToList();

            Assert.Equal(["supersedes"], published);
        }

        /// <summary>
        /// Verifies that a symmetric relation takes its label for both ends, so the two sides
        /// cannot drift apart through an edit that only touched one of them.
        /// </summary>
        [Fact]
        public void Store_Symmetric_MirrorsLabelIntoCounterpart()
        {
            Seed(nameof(Store_Symmetric_MirrorsLabelIntoCounterpart));

            var type = Sample("similar", "similar to", "whatever was left here");
            type.Symmetric = true;

            CoreHub.ObjectRelationTypeManager.Store(type);

            Assert.Equal("similar to", RelationRegistry.GetType("similar").InverseLabel);
        }

        /// <summary>
        /// Verifies that storing a known key overwrites the stored definition rather than
        /// adding a second row, which is what lets the editor state a whole definition without
        /// knowing whether the key was already taken.
        /// </summary>
        [Fact]
        public void Store_KnownKey_OverwritesDefinition()
        {
            Seed(nameof(Store_KnownKey_OverwritesDefinition));

            CoreHub.ObjectRelationTypeManager.Store(Sample("relates", "relates to", "relates to"));

            var edited = CoreHub.ObjectRelationTypeManager.GetRelationType("relates");
            edited.Label = "refers to";
            edited.Active = false;

            CoreHub.ObjectRelationTypeManager.Store(edited);

            Assert.Single(CoreHub.ObjectRelationTypeManager.GetRelationTypes());
            Assert.Equal("refers to", RelationRegistry.GetType("relates").Label);
            Assert.False(RelationRegistry.GetType("relates").Active);
        }

        /// <summary>
        /// Verifies that a dropped relation leaves the registry as well, so it is no longer
        /// offered anywhere.
        /// </summary>
        [Fact]
        public void Remove_UnpublishesType()
        {
            Seed(nameof(Remove_UnpublishesType));

            CoreHub.ObjectRelationTypeManager.Store(Sample("temporary", "temporary", "temporary"));

            Assert.True(CoreHub.ObjectRelationTypeManager.Remove("temporary"));
            Assert.Null(RelationRegistry.GetType("temporary"));
            Assert.Empty(CoreHub.ObjectRelationTypeManager.GetRelationTypes());
        }

        /// <summary>
        /// Verifies that the classes a relation accepts survive the round trip, since they are
        /// what the framework validates a target against.
        /// </summary>
        [Fact]
        public void Store_KeepsAcceptedTargetClasses()
        {
            Seed(nameof(Store_KeepsAcceptedTargetClasses));

            var type = Sample("affects", "affects", "is affected by");
            type.TargetClasses = ["Asset", "Change"];

            CoreHub.ObjectRelationTypeManager.Store(type);

            Assert.Equal(["Asset", "Change"], RelationRegistry.GetType("affects").TargetClasses);
        }

        /// <summary>
        /// Creates a relation definition of the kind the editor writes.
        /// </summary>
        /// <param name="key">The stable wire key.</param>
        /// <param name="label">How the relation reads from its source.</param>
        /// <param name="inverse">How it reads from its target.</param>
        /// <returns>The definition.</returns>
        private static ObjectRelationType Sample(string key, string label, string inverse) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Label = label,
            InverseLabel = inverse,
            System = RelationSystem.Object,
            Cardinality = RelationCardinality.ManyToMany,
            Effect = RelationEffect.None,
            Active = true,
            Icon = "link",
            Order = 1
        };
    }
}
