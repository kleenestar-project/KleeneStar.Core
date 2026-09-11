using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectProgressManager"/> —
    /// the rollup that turns the relation effect <c>AggregatesProgress</c> into a number.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectProgressManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1101");
        private static readonly Guid ClassId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1102");
        private static readonly Guid FieldId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1103");
        private static readonly Guid TodoId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1104");
        private static readonly Guid ProgressId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1105");
        private static readonly Guid DoneId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1106");
        private static readonly Guid TodoCategoryId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1107");
        private static readonly Guid ProgressCategoryId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1108");
        private static readonly Guid DoneCategoryId = Guid.Parse("6D1E4A70-9C2B-4E85-A1F3-0B7C5D9E1109");

        /// <summary>
        /// Seeds a class carrying a workflow-backed field with the three states the progress of
        /// an object is read from, and publishes an aggregating and an informational relation.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-progress", Name = "progress" });
                db.Classes.Add(new Class { Id = ClassId, Name = "Task", WorkspaceId = WorkspaceId });

                db.StatusCategories.Add(new StatusCategory { Id = TodoCategoryId, Name = "ToDo" });
                db.StatusCategories.Add(new StatusCategory { Id = ProgressCategoryId, Name = "In Progress" });
                db.StatusCategories.Add(new StatusCategory { Id = DoneCategoryId, Name = "Done" });

                db.Statuses.Add(new Status { Id = TodoId, Name = "ToDo", ClassId = ClassId, CategoryId = TodoCategoryId, State = StatusState.Active });
                db.Statuses.Add(new Status { Id = ProgressId, Name = "In Progress", ClassId = ClassId, CategoryId = ProgressCategoryId, State = StatusState.Active });
                db.Statuses.Add(new Status { Id = DoneId, Name = "Done", ClassId = ClassId, CategoryId = DoneCategoryId, State = StatusState.Active });

                db.Fields.Add(new Field
                {
                    Id = FieldId,
                    Name = "Status",
                    ClassId = ClassId,
                    FieldType = FieldType.Workflow,
                    State = FieldState.Active
                });

                db.SaveChanges();
            }

            PublishRelations();
        }

        /// <summary>
        /// Publishes the aggregating relation the rollup follows and an informational one it
        /// must ignore. The keys are arbitrary: nothing in the code knows a relation by name.
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
                Cardinality = RelationCardinality.OneToMany,
                Effect = effect,
                Active = true,
                Order = order
            });

            publish("parent", RelationEffect.AggregatesProgress, 1);
            publish("references", RelationEffect.None, 2);
        }

        /// <summary>
        /// Creates an object of the seeded class, optionally in a state.
        /// </summary>
        /// <param name="key">The object key.</param>
        /// <param name="statusId">The state to place it in, or null for none.</param>
        /// <returns>The id of the created object.</returns>
        private static Guid Object(string key, Guid? statusId = null)
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

            if (statusId.HasValue)
            {
                CoreHub.ValueManager.Add(new Value
                {
                    Id = Guid.NewGuid(),
                    ObjectId = id,
                    FieldId = FieldId,
                    Data = statusId.Value.ToString()
                });
            }

            return id;
        }

        /// <summary>
        /// Relates two objects, the first aggregating the second unless another type is named.
        /// </summary>
        /// <param name="parentId">The aggregating end.</param>
        /// <param name="childId">The counted end.</param>
        /// <param name="typeKey">The relation type.</param>
        /// <param name="status">The lifecycle state of the relation.</param>
        private static void Aggregate(Guid parentId, Guid childId, string typeKey = "parent", RelationStatus status = RelationStatus.Active)
        {
            CoreHub.ObjectRelationManager.Add(new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = typeKey,
                Direction = RelationDirection.Bidirectional,
                Status = status,
                SourceObjectId = parentId,
                TargetObjectId = childId
            });
        }

        /// <summary>
        /// Verifies the reading an object without children has always had: its own state, which
        /// is what the plan views show and what the rollup must not change.
        /// </summary>
        [Fact]
        public void GetProgress_WithoutChildren_ReportsItsOwnState()
        {
            Seed(nameof(GetProgress_WithoutChildren_ReportsItsOwnState));

            var todo = Object("PRG-TODO", TodoId);
            var running = Object("PRG-RUNNING", ProgressId);
            var done = Object("PRG-DONE", DoneId);

            Assert.Equal(0, CoreHub.ObjectProgressManager.GetProgress(todo).Percent);
            Assert.Equal(50, CoreHub.ObjectProgressManager.GetProgress(running).Percent);
            Assert.Equal(100, CoreHub.ObjectProgressManager.GetProgress(done).Percent);
            Assert.False(CoreHub.ObjectProgressManager.GetProgress(done).Aggregated);
        }

        /// <summary>
        /// Verifies the feature: a parent reports what its children have come to rather than its
        /// own state, and says how many of them are finished.
        /// </summary>
        [Fact]
        public void GetProgress_AveragesTheChildren()
        {
            Seed(nameof(GetProgress_AveragesTheChildren));

            // the parent itself is untouched - a container is never "in progress" on its own
            var parent = Object("PRG-PARENT", TodoId);
            var first = Object("PRG-C1", DoneId);
            var second = Object("PRG-C2", ProgressId);
            var third = Object("PRG-C3", TodoId);

            Aggregate(parent, first);
            Aggregate(parent, second);
            Aggregate(parent, third);

            var progress = CoreHub.ObjectProgressManager.GetProgress(parent);

            // (100 + 50 + 0) / 3
            Assert.Equal(50, progress.Percent);
            Assert.True(progress.Aggregated);
            Assert.Equal(3, progress.Contributions.Count);
            Assert.Equal(1, progress.Completed);
        }

        /// <summary>
        /// Verifies that the rollup summarises a tree from the bottom up: a child that
        /// aggregates counts with its own rolled-up figure, not with the state of its row.
        /// </summary>
        [Fact]
        public void GetProgress_RollsUpThroughSeveralLevels()
        {
            Seed(nameof(GetProgress_RollsUpThroughSeveralLevels));

            var root = Object("PRG-ROOT", TodoId);
            var branch = Object("PRG-BRANCH", TodoId);
            var leafDone = Object("PRG-LEAF-1", DoneId);
            var leafOpen = Object("PRG-LEAF-2", TodoId);
            var sibling = Object("PRG-SIBLING", DoneId);

            Aggregate(root, branch);
            Aggregate(root, sibling);
            Aggregate(branch, leafDone);
            Aggregate(branch, leafOpen);

            // the branch is (100 + 0) / 2 = 50, so the root is (50 + 100) / 2
            Assert.Equal(50, CoreHub.ObjectProgressManager.GetProgress(branch).Percent);
            Assert.Equal(75, CoreHub.ObjectProgressManager.GetProgress(root).Percent);
        }

        /// <summary>
        /// Verifies which relations count: only the ones whose type declares the aggregation,
        /// and only in the direction it runs — a child does not report its parent.
        /// </summary>
        [Fact]
        public void GetProgress_CountsOnlyAggregatingRelationsFromTheParentEnd()
        {
            Seed(nameof(GetProgress_CountsOnlyAggregatingRelationsFromTheParentEnd));

            var parent = Object("PRG-P", TodoId);
            var child = Object("PRG-C", DoneId);
            var mentioned = Object("PRG-M", DoneId);

            Aggregate(parent, child);
            Aggregate(parent, mentioned, typeKey: "references");

            var fromParent = CoreHub.ObjectProgressManager.GetProgress(parent);
            var fromChild = CoreHub.ObjectProgressManager.GetProgress(child);

            Assert.Single(fromParent.Contributions);
            Assert.Equal(100, fromParent.Percent);

            // the child is the counted end, not a counting one
            Assert.False(fromChild.Aggregated);
        }

        /// <summary>
        /// Verifies that an obsolete relation counts nothing: it is kept for the history, and a
        /// child that was removed from a parent must stop moving that parent's bar.
        /// </summary>
        [Fact]
        public void GetProgress_IgnoresObsoleteRelations()
        {
            Seed(nameof(GetProgress_IgnoresObsoleteRelations));

            var parent = Object("PRG-OBS-P", TodoId);
            var counted = Object("PRG-OBS-C", DoneId);
            var dropped = Object("PRG-OBS-D", TodoId);

            Aggregate(parent, counted);
            Aggregate(parent, dropped, status: RelationStatus.Obsolete);

            var progress = CoreHub.ObjectProgressManager.GetProgress(parent);

            Assert.Single(progress.Contributions);
            Assert.Equal(100, progress.Percent);
        }

        /// <summary>
        /// Verifies that two objects aggregating each other are survived rather than refused —
        /// a mistake somebody made must not hang the reading of either of them.
        /// </summary>
        [Fact]
        public void GetProgress_SurvivesACycle()
        {
            Seed(nameof(GetProgress_SurvivesACycle));

            var first = Object("PRG-CYC-1", DoneId);
            var second = Object("PRG-CYC-2", DoneId);

            Aggregate(first, second);
            Aggregate(second, first);

            Assert.Equal(100, CoreHub.ObjectProgressManager.GetProgress(first).Percent);
            Assert.Equal(100, CoreHub.ObjectProgressManager.GetProgress(second).Percent);
        }

        /// <summary>
        /// Verifies that an object nobody can address answers zero rather than an error, the
        /// same reading a list gives a key that names nothing.
        /// </summary>
        [Fact]
        public void GetProgress_UnknownObject_IsZero()
        {
            Seed(nameof(GetProgress_UnknownObject_IsZero));

            var progress = CoreHub.ObjectProgressManager.GetProgress(Guid.NewGuid());

            Assert.Equal(0, progress.Percent);
            Assert.False(progress.Aggregated);
            Assert.Empty(progress.Contributions);
        }
    }
}
