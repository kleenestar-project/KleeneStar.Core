using KleeneStar.Core.WebApproval;
using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests multi-step approvals: steps are passed in order and decided by their approvers -
    /// one, all, or a quorum, accounts or group members; one rejection ends the approval and
    /// needs a reason; the requester does not decide; a step whose condition does not hold is
    /// passed over; every decision is kept; the outcome moves the object through its workflow,
    /// and reaching the request state asks for the approval.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestApprovalManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C0C0C000-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("C0C0C000-0000-4000-8000-000000000002");
        private static readonly Guid WorkflowId = Guid.Parse("C0C0C000-0000-4000-8000-000000000003");
        private static readonly Guid FieldId = Guid.Parse("C0C0C000-0000-4000-8000-000000000004");
        private static readonly Guid CategoryId = Guid.Parse("C0C0C000-0000-4000-8000-000000000005");
        private static readonly Guid DraftId = Guid.Parse("C0C0C000-0000-4000-8000-000000000006");
        private static readonly Guid AwaitingId = Guid.Parse("C0C0C000-0000-4000-8000-000000000007");
        private static readonly Guid ApprovedId = Guid.Parse("C0C0C000-0000-4000-8000-000000000008");
        private static readonly Guid RejectedId = Guid.Parse("C0C0C000-0000-4000-8000-000000000009");
        private static readonly Guid RequesterId = Guid.Parse("C0C0C000-0000-4000-8000-000000000011");
        private static readonly Guid ManagerId = Guid.Parse("C0C0C000-0000-4000-8000-000000000012");
        private static readonly Guid BoardOneId = Guid.Parse("C0C0C000-0000-4000-8000-000000000013");
        private static readonly Guid BoardTwoId = Guid.Parse("C0C0C000-0000-4000-8000-000000000014");
        private static readonly Guid OutsiderId = Guid.Parse("C0C0C000-0000-4000-8000-000000000015");
        private static readonly Guid CabId = Guid.Parse("C0C0C000-0000-4000-8000-000000000016");

        /// <summary>
        /// Seeds a change class with the workflow Draft → Awaiting approval → Approved | Rejected,
        /// five accounts and the group CAB with two of them.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "CHG", Name = "Changes" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Change", WorkspaceId = WorkspaceId });
            db.StatusCategories.Add(new StatusCategory { Id = CategoryId, Name = "ToDo", Color = "#888888", IsDefault = true });
            db.Statuses.AddRange
            (
                new Status { Id = DraftId, Name = "Draft", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active },
                new Status { Id = AwaitingId, Name = "Awaiting approval", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active },
                new Status { Id = ApprovedId, Name = "Approved", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active },
                new Status { Id = RejectedId, Name = "Rejected", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active }
            );
            db.Workflows.Add(new Workflow
            {
                Id = WorkflowId,
                Name = "Change lifecycle",
                ClassId = ClassId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = DraftId, IsStart = true },
                    new WorkflowStatus { StatusId = AwaitingId },
                    new WorkflowStatus { StatusId = ApprovedId, IsEnd = true },
                    new WorkflowStatus { StatusId = RejectedId, IsEnd = true }
                ]
            });
            db.Transitions.AddRange
            (
                new Transition { Name = "Submit", WorkflowId = WorkflowId, SourceId = DraftId, TargetId = AwaitingId, State = TransitionState.Active },
                new Transition { Name = "Approve", WorkflowId = WorkflowId, SourceId = AwaitingId, TargetId = ApprovedId, State = TransitionState.Active },
                new Transition { Name = "Reject", WorkflowId = WorkflowId, SourceId = AwaitingId, TargetId = RejectedId, State = TransitionState.Active }
            );
            db.Fields.Add(new Field { Id = FieldId, Name = "Status", ClassId = ClassId, FieldType = FieldType.Workflow, WorkflowId = WorkflowId, State = FieldState.Active });

            Identity Account(Guid id, string name) => new() { Id = id, Name = name, UserName = name, Email = $"{name}@example.com", PasswordHash = "x" };

            var requester = Account(RequesterId, "requester");
            var manager = Account(ManagerId, "manager");
            var boardOne = Account(BoardOneId, "board.one");
            var boardTwo = Account(BoardTwoId, "board.two");
            var outsider = Account(OutsiderId, "outsider");
            var cab = new Group { Id = CabId, Name = "CAB" };

            db.Identities.AddRange(requester, manager, boardOne, boardTwo, outsider);
            db.Groups.Add(cab);
            db.Set<IdentityGroupMembership>().AddRange
            (
                new IdentityGroupMembership { Identity = boardOne, Group = cab },
                new IdentityGroupMembership { Identity = boardTwo, Group = cab }
            );
            db.SaveChanges();
        }

        /// <summary>
        /// Creates a change.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="summary">The summary.</param>
        /// <returns>The object id.</returns>
        private static Guid Change(string database, string summary)
        {
            var id = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Objects.Add(new ObjectEntity(id) { Key = "CHG-" + Random.Shared.Next(1000, 9999), Summary = summary, WorkspaceId = WorkspaceId, ClassId = ClassId });
            db.SaveChanges();

            return id;
        }

        /// <summary>
        /// Adds a policy.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="configure">Changes the policy before it is added.</param>
        /// <returns>The policy.</returns>
        private static ApprovalPolicy Policy(string steps, Action<ApprovalPolicy> configure = null)
        {
            var policy = new ApprovalPolicy
            {
                Name = "policy " + Guid.NewGuid().ToString("N")[..6],
                WorkspaceId = WorkspaceId,
                ClassId = ClassId,
                Steps = steps,
                State = ApprovalPolicyState.Active
            };

            configure?.Invoke(policy);
            CoreHub.ApprovalManager.AddPolicy(policy);

            return policy;
        }

        /// <summary>
        /// Returns the state an object's workflow field holds.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The value.</returns>
        private static string StateOf(Guid objectId)
        {
            return CoreHub.ValueManager.GetValue(objectId, FieldId)?.Data;
        }

        /// <summary>
        /// A policy is refused with every problem: an unreadable line, an unknown approver, a
        /// broken condition, an unreachable quorum, a state of another workflow.
        /// </summary>
        [Fact]
        public void Validate_ReportsEveryProblem()
        {
            Seed(nameof(Validate_ReportsEveryProblem));

            var problems = CoreHub.ApprovalManager.Validate(new ApprovalPolicy
            {
                Name = "broken",
                WorkspaceId = WorkspaceId,
                ClassId = ClassId,
                RequestStatusId = Guid.NewGuid(),
                Steps = "no colon here\nBoard: 3 of manager, requester\nManager: any nobody.known\nLegal: any manager if Summary ~~"
            });

            Assert.Contains(problems, x => x.StartsWith("Line 1:", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 2:", StringComparison.Ordinal) && x.Contains("3 approvals", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 3:", StringComparison.Ordinal) && x.Contains("nobody.known", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 4:", StringComparison.Ordinal) && x.Contains("WQL", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.Contains("request state", StringComparison.Ordinal));

            Assert.Empty(CoreHub.ApprovalManager.Validate(new ApprovalPolicy { Name = "cab", WorkspaceId = WorkspaceId, ClassId = ClassId, Steps = "Board: 2 of group:CAB within 48h" }));
        }

        /// <summary>
        /// The steps are passed in order: the board decides only after the manager, needs two of
        /// its members, and the approval ends approved with every decision kept.
        /// </summary>
        [Fact]
        public void Steps_ArePassedInOrder()
        {
            var database = nameof(Steps_ArePassedInOrder);
            Seed(database);

            var objectId = Change(database, "Upgrade the database");
            var policy = Policy("Manager: any manager\nBoard: 2 of group:CAB within 72h");
            var approval = CoreHub.ApprovalManager.Request(objectId, policy.Id, RequesterId, "please");

            Assert.Equal(ApprovalState.Pending, approval.State);
            Assert.True(CoreHub.ApprovalManager.IsApprover(approval, ManagerId));
            Assert.False(CoreHub.ApprovalManager.IsApprover(approval, BoardOneId));
            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Decide(approval.Id, BoardOneId, ApprovalVerdict.Approve, null));

            approval = CoreHub.ApprovalManager.Decide(approval.Id, ManagerId, ApprovalVerdict.Approve, "fine by me");
            Assert.Equal(1, approval.CurrentStep);
            Assert.True(CoreHub.ApprovalManager.IsApprover(approval, BoardOneId));
            Assert.False(CoreHub.ApprovalManager.IsApprover(approval, OutsiderId));

            approval = CoreHub.ApprovalManager.Decide(approval.Id, BoardOneId, ApprovalVerdict.Approve, null);
            Assert.Equal(ApprovalState.Pending, approval.State);

            // one vote per approver and step
            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Decide(approval.Id, BoardOneId, ApprovalVerdict.Approve, null));

            approval = CoreHub.ApprovalManager.Decide(approval.Id, BoardTwoId, ApprovalVerdict.Approve, null);
            Assert.Equal(ApprovalState.Approved, approval.State);
            Assert.NotNull(approval.Completed);
            Assert.Equal(3, approval.Decisions.Count);
            Assert.Contains(approval.Decisions, x => x.IdentityId == ManagerId && x.Reason == "fine by me" && x.Step == 0);
            Assert.True(CoreHub.ApprovalManager.IsGranted(objectId));
        }

        /// <summary>
        /// A rejection needs a reason and ends the approval at once.
        /// </summary>
        [Fact]
        public void Rejection_NeedsAReasonAndEnds()
        {
            var database = nameof(Rejection_NeedsAReasonAndEnds);
            Seed(database);

            var objectId = Change(database, "Risky change");
            var policy = Policy("Board: all group:CAB, manager");
            var approval = CoreHub.ApprovalManager.Request(objectId, policy.Id, RequesterId, null);

            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Decide(approval.Id, ManagerId, ApprovalVerdict.Reject, " "));

            approval = CoreHub.ApprovalManager.Decide(approval.Id, ManagerId, ApprovalVerdict.Reject, "No rollback plan.");

            Assert.Equal(ApprovalState.Rejected, approval.State);
            Assert.False(CoreHub.ApprovalManager.IsGranted(objectId));
            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Decide(approval.Id, BoardOneId, ApprovalVerdict.Approve, null));
        }

        /// <summary>
        /// "all" needs every named account and one member of every named group.
        /// </summary>
        [Fact]
        public void All_NeedsEveryAccountAndEveryGroup()
        {
            var database = nameof(All_NeedsEveryAccountAndEveryGroup);
            Seed(database);

            var objectId = Change(database, "Joint decision");
            var policy = Policy("Board: all group:CAB, manager");
            var approval = CoreHub.ApprovalManager.Request(objectId, policy.Id, RequesterId, null);

            approval = CoreHub.ApprovalManager.Decide(approval.Id, BoardOneId, ApprovalVerdict.Approve, null);
            Assert.Equal(ApprovalState.Pending, approval.State);

            approval = CoreHub.ApprovalManager.Decide(approval.Id, ManagerId, ApprovalVerdict.Approve, null);
            Assert.Equal(ApprovalState.Approved, approval.State);
        }

        /// <summary>
        /// The requester does not decide their own request, unless the policy allows it.
        /// </summary>
        [Fact]
        public void Requester_DoesNotDecideUnlessAllowed()
        {
            var database = nameof(Requester_DoesNotDecideUnlessAllowed);
            Seed(database);

            var strict = Policy("Manager: any manager");
            var approval = CoreHub.ApprovalManager.Request(Change(database, "own"), strict.Id, ManagerId, null);

            Assert.False(CoreHub.ApprovalManager.IsApprover(approval, ManagerId));

            var relaxed = Policy("Manager: any manager", x => x.AllowSelfApproval = true);
            var own = CoreHub.ApprovalManager.Request(Change(database, "own too"), relaxed.Id, ManagerId, null);

            Assert.Equal(ApprovalState.Approved, CoreHub.ApprovalManager.Decide(own.Id, ManagerId, ApprovalVerdict.Approve, null).State);
        }

        /// <summary>
        /// A step whose condition does not hold is passed over; with none left, the approval is
        /// approved when it is requested. A second request under the same policy is refused
        /// while one is pending.
        /// </summary>
        [Fact]
        public void Conditions_PassStepsOver()
        {
            var database = nameof(Conditions_PassStepsOver);
            Seed(database);

            var policy = Policy("Legal: any manager if Summary ~ \"contract\"\nBoard: any group:CAB if Summary ~ \"outage\"");

            var routine = CoreHub.ApprovalManager.Request(Change(database, "Routine patch"), policy.Id, RequesterId, null);
            Assert.Equal(ApprovalState.Approved, routine.State);

            var outageId = Change(database, "Planned outage");
            var outage = CoreHub.ApprovalManager.Request(outageId, policy.Id, RequesterId, null);
            Assert.Equal(1, outage.CurrentStep);
            Assert.False(CoreHub.ApprovalManager.StepsOf(outage)[0].Applies);
            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Request(outageId, policy.Id, RequesterId, null));

            // only the requester or an administrator withdraws
            Assert.Throws<ApprovalRefusedException>(() => CoreHub.ApprovalManager.Cancel(outage.Id, OutsiderId, administrator: false));
            Assert.Equal(ApprovalState.Cancelled, CoreHub.ApprovalManager.Cancel(outage.Id, RequesterId, administrator: false).State);
        }

        /// <summary>
        /// Reaching the request state asks for the approval; the outcome moves the object to the
        /// approved or the rejected state; the validator lets a move pass only when approved.
        /// </summary>
        [Fact]
        public void Workflow_RequestsAndFollowsTheOutcome()
        {
            var database = nameof(Workflow_RequestsAndFollowsTheOutcome);
            Seed(database);
            CoreHub.ApprovalManager.Connect();

            var policy = Policy("Manager: any manager", x =>
            {
                x.RequestStatusId = AwaitingId;
                x.ApprovedStatusId = ApprovedId;
                x.RejectedStatusId = RejectedId;
            });

            var approvedId = Change(database, "Goes through");
            var validator = new ApprovalGrantedValidator();

            Assert.True(CoreHub.WorkflowManager.ExecuteTransition(approvedId, FieldId, DraftId, RequesterId).Succeeded);
            Assert.True(CoreHub.WorkflowManager.ExecuteTransition(approvedId, FieldId, AwaitingId, RequesterId).Succeeded);

            var approval = Assert.Single(CoreHub.ApprovalManager.GetApprovals(approvedId));
            Assert.Equal(policy.Id, approval.PolicyId);
            Assert.Equal(RequesterId, approval.RequestedById);
            Assert.False(validator.Evaluate(new WorkflowRuleContext { Object = CoreHub.ObjectManager.GetObject(approvedId) }));

            CoreHub.ApprovalManager.Decide(approval.Id, ManagerId, ApprovalVerdict.Approve, null);

            Assert.Contains(StateOf(approvedId), new[] { ApprovedId.ToString(), "Approved" });
            Assert.True(validator.Evaluate(new WorkflowRuleContext { Object = CoreHub.ObjectManager.GetObject(approvedId) }));
            Assert.Null(CoreHub.ApprovalManager.GetApproval(approval.Id).Outcome);

            var rejectedId = Change(database, "Does not");
            CoreHub.WorkflowManager.ExecuteTransition(rejectedId, FieldId, DraftId, RequesterId);
            CoreHub.WorkflowManager.ExecuteTransition(rejectedId, FieldId, AwaitingId, RequesterId);
            CoreHub.ApprovalManager.Decide(CoreHub.ApprovalManager.GetApprovals(rejectedId)[0].Id, ManagerId, ApprovalVerdict.Reject, "Too risky.");

            Assert.Contains(StateOf(rejectedId), new[] { RejectedId.ToString(), "Rejected" });
        }

        /// <summary>
        /// A step that takes longer than it may raises <c>approval.overdue</c> once, and a rule
        /// reacting to it is queued once.
        /// </summary>
        [Fact]
        public void Overdue_IsRaisedOncePerStep()
        {
            var database = nameof(Overdue_IsRaisedOncePerStep);
            Seed(database);

            var policy = Policy("Board: any group:CAB within 4h");
            var rule = new AutomationRule
            {
                Name = "escalate overdue",
                WorkspaceId = WorkspaceId,
                Trigger = AutomationTriggerCatalog.ApprovalOverdue,
                Actions = "escalate",
                RunAsId = ManagerId,
                State = AutomationRuleState.Active
            };

            CoreHub.AutomationManager.AddRule(rule);

            var approval = CoreHub.ApprovalManager.Request(Change(database, "Slow board"), policy.Id, RequesterId, null);

            Assert.Equal(0, CoreHub.ApprovalManager.RaiseOverdue(DateTime.UtcNow.AddHours(3)));
            Assert.Equal(1, CoreHub.ApprovalManager.RaiseOverdue(DateTime.UtcNow.AddHours(5)));
            Assert.Equal(1, CoreHub.ApprovalManager.RaiseOverdue(DateTime.UtcNow.AddHours(6)));

            Assert.Single(ModelHub.GetAutomationJobs([rule.Id], null, 10));
            Assert.Equal(ApprovalState.Pending, CoreHub.ApprovalManager.GetApproval(approval.Id).State);
        }

        /// <summary>
        /// A rule's <c>approval</c> line names a policy of its workspace and class.
        /// </summary>
        [Fact]
        public void ApprovalAction_IsValidatedAgainstThePolicies()
        {
            Seed(nameof(ApprovalAction_IsValidatedAgainstThePolicies));

            var policy = Policy("Manager: any manager");

            Assert.Empty(CoreHub.AutomationManager.Validate(new AutomationRule { Name = "ok", WorkspaceId = WorkspaceId, Trigger = AutomationTriggerCatalog.ObjectCreated, Actions = $"approval {policy.Name}" }));
            Assert.Contains(CoreHub.AutomationManager.Validate(new AutomationRule { Name = "no", WorkspaceId = WorkspaceId, Trigger = AutomationTriggerCatalog.ObjectCreated, Actions = "approval nonexistent" }), x => x.StartsWith("Line 1:", StringComparison.Ordinal));
        }

        /// <summary>
        /// The templates are valid policies once their groups exist.
        /// </summary>
        [Fact]
        public void Templates_AreReadable()
        {
            Seed(nameof(Templates_AreReadable));

            foreach (var template in ApprovalTemplates.All)
            {
                Assert.NotEmpty(ApprovalSteps.Parse(template.Steps));
                Assert.All(ApprovalSteps.Validate(template.Steps), x => Assert.Contains("unknown approver 'group:", x, StringComparison.Ordinal));
            }
        }
    }
}
