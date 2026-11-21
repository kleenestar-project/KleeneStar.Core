using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the workflow effect of relations: which of them refuse a move into
    /// a closing state, and which objects follow one that reaches it.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectRelationWorkflowRules
    {
        private static readonly Guid WorkspaceId = Guid.Parse("11110000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("11110000-0000-0000-0000-000000000002");
        private static readonly Guid WorkflowId = Guid.Parse("11110000-0000-0000-0000-000000000003");
        private static readonly Guid FieldId = Guid.Parse("11110000-0000-0000-0000-000000000004");
        private static readonly Guid TodoId = Guid.Parse("11110000-0000-0000-0000-000000000005");
        private static readonly Guid DoneId = Guid.Parse("11110000-0000-0000-0000-000000000006");
        private static readonly Guid TodoCategoryId = Guid.Parse("11110000-0000-0000-0000-000000000007");
        private static readonly Guid DoneCategoryId = Guid.Parse("11110000-0000-0000-0000-000000000008");
        private static readonly Guid BlockerId = Guid.Parse("11110000-0000-0000-0000-00000000000A");
        private static readonly Guid BlockedId = Guid.Parse("11110000-0000-0000-0000-00000000000B");

        /// <summary>
        /// Seeds a workspace whose one class carries a two-state workflow (ToDo → Done) on a
        /// workflow field, plus two objects of it, and publishes a blocking and a closing
        /// relation into the registry.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-rules", Name = "rules" });
                db.Classes.Add(new Class { Id = ClassId, Name = "Task", WorkspaceId = WorkspaceId });

                db.StatusCategories.Add(new StatusCategory { Id = TodoCategoryId, Name = "ToDo" });
                db.StatusCategories.Add(new StatusCategory { Id = DoneCategoryId, Name = "Done" });

                db.Statuses.Add(new Status { Id = TodoId, Name = "ToDo", ClassId = ClassId, CategoryId = TodoCategoryId, State = StatusState.Active });
                db.Statuses.Add(new Status { Id = DoneId, Name = "Done", ClassId = ClassId, CategoryId = DoneCategoryId, State = StatusState.Active });

                // the states participate through the join the designer writes, which is what
                // GetTargetStatuses walks
                db.Workflows.Add(new Workflow
                {
                    Id = WorkflowId,
                    Name = "flow",
                    ClassId = ClassId,
                    State = WorkflowState.Active,
                    WorkflowStatuses =
                    [
                        new WorkflowStatus { WorkflowId = WorkflowId, StatusId = TodoId, IsStart = true },
                        new WorkflowStatus { WorkflowId = WorkflowId, StatusId = DoneId, IsEnd = true }
                    ]
                });

                db.Transitions.Add(new Transition
                {
                    Id = Guid.NewGuid(),
                    Name = "finish",
                    WorkflowId = WorkflowId,
                    SourceId = TodoId,
                    TargetId = DoneId,
                    State = TransitionState.Active
                });

                db.Fields.Add(new Field
                {
                    Id = FieldId,
                    Name = "State",
                    ClassId = ClassId,
                    FieldType = FieldType.Workflow,
                    WorkflowId = WorkflowId,
                    State = FieldState.Active
                });

                db.Objects.Add(new ObjectEntity { Id = BlockerId, Key = "BLK-1", Summary = "blocker", WorkspaceId = WorkspaceId, ClassId = ClassId });
                db.Objects.Add(new ObjectEntity { Id = BlockedId, Key = "BLK-2", Summary = "blocked", WorkspaceId = WorkspaceId, ClassId = ClassId });

                db.SaveChanges();
            }

            PublishRelations();
        }

        /// <summary>
        /// Publishes a blocking and a closing relation, so the effects under test exist. They are
        /// defined here rather than assumed, which is the point of the model: nothing in the code
        /// knows a relation by name.
        /// </summary>
        private static void PublishRelations()
        {
            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "blocks",
                Label = "blocks",
                InverseLabel = "is blocked by",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Effect = RelationEffect.BlocksCompletion,
                Active = true,
                Order = 1
            });

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "duplicate",
                Label = "duplicate of",
                InverseLabel = "has duplicate",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToOne,
                Effect = RelationEffect.ClosesItem,
                Active = true,
                Order = 2
            });
        }

        /// <summary>
        /// Sets the workflow state of an object directly, bypassing the transition, so a test can
        /// arrange the state it wants to observe the rules against.
        /// </summary>
        /// <param name="objectId">The object to place.</param>
        /// <param name="status">The state to place it in.</param>
        private static void Place(Guid objectId, Status status)
        {
            CoreHub.ValueManager.Add(new Value
            {
                ObjectId = objectId,
                FieldId = FieldId,
                Data = status.Name,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Establishes a relation between the two seeded objects.
        /// </summary>
        /// <param name="typeKey">The relation to establish.</param>
        private static void Relate(string typeKey)
        {
            CoreHub.ObjectRelationManager.Add(new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = typeKey,
                SourceObjectId = BlockerId,
                TargetObjectId = BlockedId
            });
        }

        /// <summary>
        /// Returns a seeded status.
        /// </summary>
        /// <param name="statusId">The id of the status.</param>
        /// <returns>The status.</returns>
        private static Status Get(Guid statusId)
        {
            return CoreHub.StatusManager.GetStatus(statusId);
        }

        /// <summary>
        /// Verifies that an open blocker refuses the move of the object it blocks into a closing
        /// state, and names itself as the reason.
        /// </summary>
        [Fact]
        public void Transition_IntoClosingState_IsRefusedByAnOpenBlocker()
        {
            Seed(nameof(Transition_IntoClosingState_IsRefusedByAnOpenBlocker));

            Place(BlockerId, Get(TodoId));
            Place(BlockedId, Get(TodoId));
            Relate("blocks");

            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Blocked, result.Outcome);
            Assert.Equal(["BLK-1"], result.ValidationErrors);

            // the state must not have moved
            Assert.Equal("ToDo", CoreHub.ValueManager.GetValue(BlockedId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that a closed blocker holds nothing back - the relation still exists, but it
        /// no longer states anything about an object that is finished.
        /// </summary>
        [Fact]
        public void Transition_IntoClosingState_IsAllowedOnceTheBlockerIsClosed()
        {
            Seed(nameof(Transition_IntoClosingState_IsAllowedOnceTheBlockerIsClosed));

            Place(BlockerId, Get(DoneId));
            Place(BlockedId, Get(TodoId));
            Relate("blocks");

            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);
            Assert.Equal("Done", CoreHub.ValueManager.GetValue(BlockedId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that an obsolete relation refuses nothing. A relation that stopped holding is
        /// kept for the history, and history must not govern what may happen next.
        /// </summary>
        [Fact]
        public void Transition_IsNotRefusedByAnObsoleteRelation()
        {
            Seed(nameof(Transition_IsNotRefusedByAnObsoleteRelation));

            Place(BlockerId, Get(TodoId));
            Place(BlockedId, Get(TodoId));

            var relation = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = "blocks",
                Status = RelationStatus.Obsolete,
                SourceObjectId = BlockerId,
                TargetObjectId = BlockedId
            };

            CoreHub.ObjectRelationManager.Add(relation);

            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);
        }

        /// <summary>
        /// Verifies that only a move into a closing state can be refused, so a blocked object can
        /// still be worked on - it simply cannot be finished.
        /// </summary>
        [Fact]
        public void Transition_IntoOpenState_IsNeverRefused()
        {
            Seed(nameof(Transition_IntoOpenState_IsNeverRefused));

            Place(BlockerId, Get(TodoId));
            Relate("blocks");

            // the blocked object has not entered the state machine yet, so this places it on the
            // entry state - an open one, which no relation may hold back
            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, TodoId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);
        }

        /// <summary>
        /// Verifies that an object declaring itself closed with another one follows it, which is
        /// how a duplicate is settled by its original.
        /// </summary>
        [Fact]
        public void Transition_IntoClosingState_ClosesTheItemsThatFollowIt()
        {
            Seed(nameof(Transition_IntoClosingState_ClosesTheItemsThatFollowIt));

            Place(BlockerId, Get(TodoId));
            Place(BlockedId, Get(TodoId));

            // the duplicate is the source: "this item ... is closed with the target"
            Relate("duplicate");

            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);
            Assert.Equal("Done", CoreHub.ValueManager.GetValue(BlockerId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that a relation carrying no workflow effect leaves the workflow alone, which
        /// is what makes the effect a decision an administrator takes per relation.
        /// </summary>
        [Fact]
        public void Transition_IsUnaffectedByARelationWithoutEffect()
        {
            Seed(nameof(Transition_IsUnaffectedByARelationWithoutEffect));

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "references",
                Label = "references",
                InverseLabel = "is referenced by",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Effect = RelationEffect.None,
                Active = true,
                Order = 3
            });

            Place(BlockerId, Get(TodoId));
            Place(BlockedId, Get(TodoId));
            Relate("references");

            var result = CoreHub.WorkflowManager.ExecuteTransition(BlockedId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);
            Assert.Equal("ToDo", CoreHub.ValueManager.GetValue(BlockerId, FieldId)?.Data);
        }
    }
}
