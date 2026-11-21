using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the state-machine side of
    /// <see cref="KleeneStar.Core.WebManager.WorkflowManager"/>: loading a workflow with its
    /// structure, walking it to the reachable states, and executing a state change on an object.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowManagerTransition
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A1000000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("A1000000-0000-0000-0000-000000000002");
        private static readonly Guid WorkflowId = Guid.Parse("A1000000-0000-0000-0000-000000000003");
        private static readonly Guid FieldId = Guid.Parse("A1000000-0000-0000-0000-000000000004");
        private static readonly Guid ObjectId = Guid.Parse("A1000000-0000-0000-0000-000000000005");
        private static readonly Guid CategoryToDoId = Guid.Parse("A1000000-0000-0000-0000-000000000006");
        private static readonly Guid CategoryDoneId = Guid.Parse("A1000000-0000-0000-0000-000000000007");
        private static readonly Guid NewId = Guid.Parse("A1000000-0000-0000-0000-000000000008");
        private static readonly Guid ProgressId = Guid.Parse("A1000000-0000-0000-0000-000000000009");
        private static readonly Guid DoneId = Guid.Parse("A1000000-0000-0000-0000-00000000000A");

        /// <summary>
        /// Seeds a linear workflow New → In Progress → Done attached to a workflow-typed field of
        /// a class, plus one object of that class.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="workflowState">
        /// The lifecycle state of the workflow, so the guard against an unpublished definition can
        /// be exercised.
        /// </param>
        private static void Seed(string connectionString, WorkflowState workflowState = WorkflowState.Active)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wt", Name = "main" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });

            db.StatusCategories.Add(new StatusCategory { Id = CategoryToDoId, Name = "ToDo", Color = "#FF5733", IsDefault = true });
            db.StatusCategories.Add(new StatusCategory { Id = CategoryDoneId, Name = "Done", Color = "#28A745" });

            db.Statuses.AddRange
            (
                new Status { Id = NewId, Name = "New", ClassId = ClassId, CategoryId = CategoryToDoId, State = StatusState.Active },
                new Status { Id = ProgressId, Name = "In Progress", ClassId = ClassId, CategoryId = CategoryToDoId, State = StatusState.Active },
                new Status { Id = DoneId, Name = "Done", ClassId = ClassId, CategoryId = CategoryDoneId, State = StatusState.Active }
            );

            db.Workflows.Add(new Workflow
            {
                Id = WorkflowId,
                Name = "Standard Lifecycle",
                ClassId = ClassId,
                State = workflowState,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = NewId, IsStart = true },
                    new WorkflowStatus { StatusId = ProgressId },
                    new WorkflowStatus { StatusId = DoneId, IsEnd = true }
                ]
            });

            db.Transitions.AddRange
            (
                new Transition { Name = "Start Work", WorkflowId = WorkflowId, SourceId = NewId, TargetId = ProgressId, State = TransitionState.Active },
                new Transition { Name = "Resolve", WorkflowId = WorkflowId, SourceId = ProgressId, TargetId = DoneId, State = TransitionState.Active },
                new Transition { Name = "Retired", WorkflowId = WorkflowId, SourceId = NewId, TargetId = DoneId, State = TransitionState.Archived }
            );

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

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that the structural load carries the participating states with their category
        /// and the transitions with both endpoints resolved — none of which the shallow
        /// <c>GetWorkflow</c> read provides.
        /// </summary>
        [Fact]
        public void GetWorkflowWithStructure_LoadsStatesAndTransitions()
        {
            Seed(nameof(GetWorkflowWithStructure_LoadsStatesAndTransitions));

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);

            Assert.NotNull(workflow);
            Assert.Equal(3, workflow.Statuses.Count);
            Assert.Equal(3, workflow.Transitions.Count);
            Assert.Contains(workflow.Statuses, x => x.Name == "New" && x.Category?.Color == "#FF5733");
            Assert.All(workflow.Transitions, x => Assert.NotNull(x.Target));
        }

        /// <summary>
        /// Verifies that the reachable states of a state are the targets of the active transitions
        /// leaving it — an archived transition does not offer its target.
        /// </summary>
        [Fact]
        public void GetTargetStatuses_FollowsActiveTransitionsOnly()
        {
            Seed(nameof(GetTargetStatuses_FollowsActiveTransitionsOnly));

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);
            var current = workflow.Statuses.First(x => x.Id == NewId);

            var targets = CoreHub.WorkflowManager.GetTargetStatuses(workflow, current).ToList();

            Assert.Single(targets);
            Assert.Equal(ProgressId, targets[0].Id);
        }

        /// <summary>
        /// Verifies that an object which has not entered the state machine yet is offered the
        /// workflow's entry states.
        /// </summary>
        [Fact]
        public void GetTargetStatuses_WithoutCurrentStatus_ReturnsEntryStates()
        {
            Seed(nameof(GetTargetStatuses_WithoutCurrentStatus_ReturnsEntryStates));

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);

            var targets = CoreHub.WorkflowManager.GetTargetStatuses(workflow, null).ToList();

            Assert.Single(targets);
            Assert.Equal(NewId, targets[0].Id);
        }

        /// <summary>
        /// Verifies that a terminal state offers no way out.
        /// </summary>
        [Fact]
        public void GetTargetStatuses_OfTerminalState_IsEmpty()
        {
            Seed(nameof(GetTargetStatuses_OfTerminalState_IsEmpty));

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);
            var current = workflow.Statuses.First(x => x.Id == DoneId);

            Assert.Empty(CoreHub.WorkflowManager.GetTargetStatuses(workflow, current));
        }

        /// <summary>
        /// Verifies that the persisted payload resolves to a state by its normalized name, which
        /// is how the seeded slugs (<c>in_progress</c>) reach <c>In Progress</c>.
        /// </summary>
        [Fact]
        public void ResolveStatus_MatchesNormalizedName()
        {
            Seed(nameof(ResolveStatus_MatchesNormalizedName));

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(WorkflowId);

            Assert.Equal(ProgressId, CoreHub.WorkflowManager.ResolveStatus(workflow, "in_progress")?.Id);
            Assert.Equal(DoneId, CoreHub.WorkflowManager.ResolveStatus(workflow, DoneId.ToString())?.Id);
            Assert.Null(CoreHub.WorkflowManager.ResolveStatus(workflow, "nonsense"));
        }

        /// <summary>
        /// Verifies that a state change along an active transition writes the new state to the
        /// object's workflow field.
        /// </summary>
        [Fact]
        public void ExecuteTransition_AlongActiveTransition_WritesValue()
        {
            Seed(nameof(ExecuteTransition_AlongActiveTransition_WritesValue));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, ProgressId, Guid.Empty);

            Assert.True(result.Succeeded);
            Assert.Equal(NewId, result.Source?.Id);
            Assert.Equal(ProgressId, result.Target?.Id);
            Assert.Equal("Start Work", result.Transition?.Name);
            Assert.Equal("In Progress", CoreHub.ValueManager.GetValue(ObjectId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that an object without a state is placed on an entry state, creating the value
        /// row that did not exist yet.
        /// </summary>
        [Fact]
        public void ExecuteTransition_WithoutCurrentStatus_EntersAtStartState()
        {
            Seed(nameof(ExecuteTransition_WithoutCurrentStatus_EntersAtStartState));

            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, NewId, Guid.Empty);

            Assert.True(result.Succeeded);
            Assert.Null(result.Source);
            Assert.Null(result.Transition);
            Assert.Equal("New", CoreHub.ValueManager.GetValue(ObjectId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that a state no active transition reaches is refused and the value is left
        /// untouched.
        /// </summary>
        [Fact]
        public void ExecuteTransition_WithoutTransition_IsRefused()
        {
            Seed(nameof(ExecuteTransition_WithoutTransition_IsRefused));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            // New → Done exists, but only as an archived transition
            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, DoneId, Guid.Empty);

            Assert.False(result.Succeeded);
            Assert.Equal(WorkflowTransitionOutcome.NotAllowed, result.Outcome);
            Assert.Equal("New", CoreHub.ValueManager.GetValue(ObjectId, FieldId)?.Data);
        }

        /// <summary>
        /// Verifies that requesting the state the object already carries is reported as a no-op
        /// rather than as a failure.
        /// </summary>
        [Fact]
        public void ExecuteTransition_ToCurrentStatus_IsUnchanged()
        {
            Seed(nameof(ExecuteTransition_ToCurrentStatus_IsUnchanged));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, NewId, Guid.Empty);

            Assert.False(result.Succeeded);
            Assert.Equal(WorkflowTransitionOutcome.Unchanged, result.Outcome);
        }

        /// <summary>
        /// Verifies that a workflow that no longer governs objects refuses state changes: an
        /// archived definition is read-only.
        /// </summary>
        [Fact]
        public void ExecuteTransition_OnArchivedWorkflow_IsRefused()
        {
            Seed(nameof(ExecuteTransition_OnArchivedWorkflow_IsRefused), WorkflowState.Archived);

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, ProgressId, Guid.Empty);

            Assert.False(result.Succeeded);
            Assert.Equal(WorkflowTransitionOutcome.NotAllowed, result.Outcome);
        }

        /// <summary>
        /// Verifies that a completed state change raises the transition event, which is what the
        /// audit and integration hooks subscribe to.
        /// </summary>
        [Fact]
        public void ExecuteTransition_RaisesTransitionExecuted()
        {
            Seed(nameof(ExecuteTransition_RaisesTransitionExecuted));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            WorkflowTransitionResult raised = null;
            CoreHub.WorkflowManager.TransitionExecuted += (_, r) => raised = r;

            CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, ProgressId, Guid.Empty);

            Assert.NotNull(raised);
            Assert.Equal(ProgressId, raised.Target?.Id);
        }

        /// <summary>
        /// Verifies that an unknown state is reported as not found rather than silently ignored.
        /// </summary>
        [Fact]
        public void ExecuteTransition_WithUnknownStatus_IsNotFound()
        {
            Seed(nameof(ExecuteTransition_WithUnknownStatus_IsNotFound));

            var result = CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, Guid.NewGuid(), Guid.Empty);

            Assert.False(result.Succeeded);
            Assert.Equal(WorkflowTransitionOutcome.NotFound, result.Outcome);
        }

        /// <summary>
        /// Verifies that a state change is recorded in the object's history as a single commit of
        /// type <see cref="CommitType.Transitioned"/> — the state write and the stamp the follow-up
        /// puts on the object are one action, and the history has to say which one it was.
        /// </summary>
        [Fact]
        public void ExecuteTransition_RecordsOneTransitionCommit()
        {
            Seed(nameof(ExecuteTransition_RecordsOneTransitionCommit));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            var before = CoreHub.CommitManager.GetHistory(ObjectId).Count();

            CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, ProgressId, Guid.Empty);

            var history = CoreHub.CommitManager.GetHistory(ObjectId).ToList();

            Assert.Equal(before + 1, history.Count);
            Assert.Equal(CommitType.Transitioned, history[0].Type);

            var change = Assert.Single(history[0].Changes, x => x.FieldId == FieldId);
            Assert.Equal("New", change.OldValue);
            Assert.Equal("In Progress", change.NewValue);
        }

        /// <summary>
        /// Verifies that a refused state change leaves no trace in the history: a transition the
        /// workflow does not allow never happened.
        /// </summary>
        [Fact]
        public void ExecuteTransition_WhenRefused_RecordsNoCommit()
        {
            Seed(nameof(ExecuteTransition_WhenRefused_RecordsNoCommit));

            CoreHub.ValueManager.Add(new Value { ObjectId = ObjectId, FieldId = FieldId, Data = "New" });

            var before = CoreHub.CommitManager.GetHistory(ObjectId).Count();

            // the New -> Done transition is archived, so the move is not allowed
            CoreHub.WorkflowManager.ExecuteTransition(ObjectId, FieldId, DoneId, Guid.Empty);

            Assert.Equal(before, CoreHub.CommitManager.GetHistory(ObjectId).Count());
        }
    }
}
