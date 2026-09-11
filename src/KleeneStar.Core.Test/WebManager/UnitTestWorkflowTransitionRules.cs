using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the configurable stages of a transition: the guards that decide
    /// whether it may be taken, the validators that decide whether it is accepted, the screen
    /// values both of them read, and the post functions that run once it has been applied.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowTransitionRules
    {
        private static readonly Guid WorkspaceId = Guid.Parse("B2000000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("B2000000-0000-0000-0000-000000000002");
        private static readonly Guid WorkflowId = Guid.Parse("B2000000-0000-0000-0000-000000000003");
        private static readonly Guid FieldId = Guid.Parse("B2000000-0000-0000-0000-000000000004");
        private static readonly Guid ObjectId = Guid.Parse("B2000000-0000-0000-0000-000000000005");
        private static readonly Guid CategoryToDoId = Guid.Parse("B2000000-0000-0000-0000-000000000006");
        private static readonly Guid CategoryDoneId = Guid.Parse("B2000000-0000-0000-0000-000000000007");
        private static readonly Guid NewId = Guid.Parse("B2000000-0000-0000-0000-000000000008");
        private static readonly Guid ProgressId = Guid.Parse("B2000000-0000-0000-0000-000000000009");
        private static readonly Guid TransitionId = Guid.Parse("B2000000-0000-0000-0000-00000000000A");
        private static readonly Guid IdentityId = Guid.Parse("B2000000-0000-0000-0000-00000000000B");

        /// <summary>
        /// Seeds one workflow with a single transition New → In Progress, an object sitting in
        /// New, and the identity making the moves.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="guards">The guard expression of the transition.</param>
        /// <param name="validators">The validator expression of the transition.</param>
        /// <param name="postFunctions">The post functions of the transition.</param>
        private static void Seed(string connectionString, string guards = null, string validators = null, params string[] postFunctions)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-rules", Name = "main" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });

            db.Identities.Add(new Identity
            {
                Id = IdentityId,
                Name = "Rule Tester",
                UserName = "rule.tester",
                Email = "rule.tester@kleenestar.test",
                PasswordHash = "$seed$v1$test",
                State = IdentityState.Active
            });

            db.StatusCategories.Add(new StatusCategory { Id = CategoryToDoId, Name = "ToDo", IsDefault = true });
            db.StatusCategories.Add(new StatusCategory { Id = CategoryDoneId, Name = "Done" });

            db.Statuses.AddRange
            (
                new Status { Id = NewId, Name = "New", ClassId = ClassId, CategoryId = CategoryToDoId, State = StatusState.Active },
                new Status { Id = ProgressId, Name = "In Progress", ClassId = ClassId, CategoryId = CategoryToDoId, State = StatusState.Active }
            );

            db.Workflows.Add(new Workflow
            {
                Id = WorkflowId,
                Name = "Standard Lifecycle",
                ClassId = ClassId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = NewId, IsStart = true },
                    new WorkflowStatus { StatusId = ProgressId }
                ]
            });

            db.Transitions.Add(new Transition
            {
                Id = TransitionId,
                Name = "Start Work",
                WorkflowId = WorkflowId,
                SourceId = NewId,
                TargetId = ProgressId,
                State = TransitionState.Active,
                GuardExpression = guards,
                ValidatorExpression = validators,
                PostFunctionKeys = [.. postFunctions]
            });

            db.Fields.Add(new Field
            {
                Id = FieldId,
                Name = "Status",
                ClassId = ClassId,
                FieldType = FieldType.Workflow,
                WorkflowId = WorkflowId,
                State = FieldState.Active
            });

            db.Objects.Add(new Model.Entities.Object
            {
                Id = ObjectId,
                Key = "INC-1",
                Summary = "Printer on fire",
                WorkspaceId = WorkspaceId,
                ClassId = ClassId
            });

            db.Values.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            db.SaveChanges();
        }

        /// <summary>
        /// Assigns the seeded object to somebody, so the conditions about the assignee can be
        /// exercised from both sides.
        /// </summary>
        /// <param name="identityId">The identity to assign to, or null to clear it.</param>
        private static void Assign(Guid? identityId)
        {
            var @object = CoreHub.ObjectManager.GetObject(ObjectId);
            @object.AssigneeId = identityId;

            CoreHub.ObjectManager.Update(@object);
        }

        /// <summary>
        /// Moves the seeded object to In Progress.
        /// </summary>
        /// <param name="screen">What the screen of the transition carries.</param>
        /// <returns>The outcome.</returns>
        private static WorkflowTransitionResult Move(IReadOnlyDictionary<string, string> screen = null)
        {
            return CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, ProgressId, IdentityId, screen);
        }

        /// <summary>
        /// Verifies that a transition without configured rules behaves exactly as it did before
        /// they existed — silence is unconditional, or every workflow that predates the feature
        /// would be impassable.
        /// </summary>
        [Fact]
        public void Execute_WithoutRules_IsUnchanged()
        {
            Seed(nameof(Execute_WithoutRules_IsUnchanged));

            Assert.Equal(WorkflowTransitionOutcome.Executed, Move().Outcome);
        }

        /// <summary>
        /// Verifies that a guard refuses the move, and that satisfying it lets the same move
        /// through — the condition is asked of the object as it is, not of how it was stored.
        /// </summary>
        [Fact]
        public void Execute_Guard_RefusesUntilItHolds()
        {
            Seed(nameof(Execute_Guard_RefusesUntilItHolds), guards: "assignee.set");

            Assert.Equal(WorkflowTransitionOutcome.NotAllowed, Move().Outcome);

            Assign(IdentityId);

            Assert.Equal(WorkflowTransitionOutcome.Executed, Move().Outcome);
        }

        /// <summary>
        /// Verifies the two dimensions of the expression on a guard: every term of a conjunction
        /// has to hold, and one holding conjunction is enough.
        /// </summary>
        [Fact]
        public void Execute_Guard_ReadsTheExpressionAsWritten()
        {
            // (assigned and made by the assignee) or (made by the creator)
            Seed(nameof(Execute_Guard_ReadsTheExpressionAsWritten), guards: "assignee.set;actor.is.assignee|actor.is.creator");

            // neither: not assigned, and the object was created by nobody
            Assert.Equal(WorkflowTransitionOutcome.NotAllowed, Move().Outcome);

            // the first conjunction, both terms
            Assign(IdentityId);

            Assert.Equal(WorkflowTransitionOutcome.Executed, Move().Outcome);
        }

        /// <summary>
        /// Verifies that a guard hides the move rather than only refusing it: what the dropdown
        /// offers and what the endpoint accepts have to be the same answer, or the refusal
        /// arrives after the click.
        /// </summary>
        [Fact]
        public void GetOfferedStatuses_LeavesOutWhatTheGuardsRefuse()
        {
            Seed(nameof(GetOfferedStatuses_LeavesOutWhatTheGuardsRefuse), guards: "assignee.set");

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);
            var @object = CoreHub.ObjectManager.GetObject(ObjectId);
            var field = CoreHub.FieldManager.GetField(FieldId);
            var current = workflow.Statuses.First(x => x.Id == NewId);

            Assert.Empty(CoreHub.WorkflowManager.GetOfferedStatuses(workflow, current, @object, field, IdentityId));

            // ...while the state machine itself still says the state is reachable
            Assert.Single(CoreHub.WorkflowManager.GetTargetStatuses(workflow, current));

            Assign(IdentityId);

            // ...and the object has to be re-read: the guards judge the object they are handed,
            // not the row behind it, which is what lets a caller ask the question about a state
            // the object is not in yet
            Assert.Single(CoreHub.WorkflowManager.GetOfferedStatuses(workflow, current, CoreHub.ObjectManager.GetObject(ObjectId), field, IdentityId));
        }

        /// <summary>
        /// Verifies that a validator refuses the move and says what has to happen first, rather
        /// than hiding it: the difference between a guard and a validator is what its failure
        /// means to the person making the move.
        /// </summary>
        [Fact]
        public void Execute_Validator_RefusesAndNamesWhatIsMissing()
        {
            Seed(nameof(Execute_Validator_RefusesAndNamesWhatIsMissing), validators: "description.set");

            var refused = Move();

            Assert.Equal(WorkflowTransitionOutcome.ValidationFailed, refused.Outcome);
            Assert.Contains("kleenestar.core:workflow.rule.description.set.message", refused.ValidationErrors);
        }

        /// <summary>
        /// Verifies that a validator reads what the screen carries, before anything is written —
        /// which is the whole point of a transition screen: a move that demands a note cannot be
        /// made without one, and the note does not have to be stored first.
        /// </summary>
        [Fact]
        public void Execute_Validator_ReadsTheScreen()
        {
            Seed(nameof(Execute_Validator_ReadsTheScreen), validators: "screen.note.given");

            Assert.Equal(WorkflowTransitionOutcome.ValidationFailed, Move().Outcome);

            var accepted = Move(new Dictionary<string, string> { ["Note"] = "Picked it up." });

            Assert.Equal(WorkflowTransitionOutcome.Executed, accepted.Outcome);
        }

        /// <summary>
        /// Verifies that the post functions the transition names run once it has been applied,
        /// in the order they were administered, and that they see the screen as well.
        /// </summary>
        [Fact]
        public void Execute_PostFunctions_RunAfterTheMove()
        {
            Seed
            (
                nameof(Execute_PostFunctions_RunAfterTheMove),
                postFunctions: ["assign.to.actor", "screen.note.comment"]
            );

            var result = Move(new Dictionary<string, string> { ["comment"] = "Taking this." });

            Assert.Equal(WorkflowTransitionOutcome.Executed, result.Outcome);

            // ...the object is now assigned to whoever moved it
            Assert.Equal(IdentityId, CoreHub.ObjectManager.GetObject(ObjectId)?.AssigneeId);

            // ...and what was typed on the screen is in the discussion rather than only in the
            // commit of the state change
            var comments = CoreHub.CommentManager.GetComments(ObjectId).ToList();

            Assert.Single(comments);
            Assert.Equal("Taking this.", comments[0].Content);
            Assert.Equal(IdentityId, comments[0].AuthorId);
        }

        /// <summary>
        /// Verifies that a rule key naming something no longer installed does not break the
        /// workflow: a guard term fails to hold, so the move is refused rather than silently
        /// widened, and an unknown post function is skipped rather than throwing.
        /// </summary>
        [Fact]
        public void Execute_UnknownKeys_FailClosedForGuardsAndAreSkippedForActions()
        {
            Seed
            (
                nameof(Execute_UnknownKeys_FailClosedForGuardsAndAreSkippedForActions),
                guards: "gone.with.its.plugin",
                postFunctions: ["also.gone"]
            );

            Assert.Equal(WorkflowTransitionOutcome.NotAllowed, Move().Outcome);

            // with the guard gone the same transition runs, and the unknown action is skipped
            using var db = CoreHubFixture.CreateDbContext(nameof(Execute_UnknownKeys_FailClosedForGuardsAndAreSkippedForActions));
            var transition = db.Transitions.First(x => x.Id == TransitionId);
            transition.GuardExpression = null;
            db.SaveChanges();

            Assert.Equal(WorkflowTransitionOutcome.Executed, Move().Outcome);
        }
    }
}
