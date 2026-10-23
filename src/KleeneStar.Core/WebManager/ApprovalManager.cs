using KleeneStar.Core.WebApproval;
using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebAutomation.Actions;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Multi-step approvals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An approval passes the steps of its policy in order; a step whose condition did not hold
    /// when the approval was requested is passed over. A step is decided when enough of its
    /// approvers agreed (<see cref="ApprovalMode"/>); one rejection ends the approval. Every
    /// decision is stored with its approver, time and reason, at most one per approver and
    /// step, and two approvers deciding at once cannot both complete a step - the approval's
    /// version token makes the later one decide again on the new state.
    /// </para>
    /// <para>
    /// Separation of duties: unless the policy allows it, the requester does not decide. The
    /// approvers of a step learn of it through their notification center; the outcome moves the
    /// object to the policy's approved or rejected state, as the last decider. Rules react to
    /// <c>approval.requested</c>, <c>approval.completed</c> and <c>approval.overdue</c>.
    /// </para>
    /// </remarks>
    public sealed class ApprovalManager : IApprovalManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private bool _connected;

        /// <inheritdoc/>
        public event EventHandler<ApprovalPolicy> PolicyAdded;

        /// <inheritdoc/>
        public event EventHandler<ApprovalPolicy> PolicyUpdated;

        /// <inheritdoc/>
        public event EventHandler<ApprovalPolicy> PolicyRemoved;

        /// <inheritdoc/>
        public event EventHandler<Approval> ApprovalRequested;

        /// <inheritdoc/>
        public event EventHandler<ApprovalDecision> ApprovalDecided;

        /// <inheritdoc/>
        public event EventHandler<Approval> ApprovalCompleted;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ApprovalManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ApprovalPolicy> GetPolicies(Guid? workspaceId)
        {
            return ModelHub.GetApprovalPolicies(workspaceId);
        }

        /// <inheritdoc/>
        public ApprovalPolicy GetPolicy(Guid policyId)
        {
            return ModelHub.GetApprovalPolicy(policyId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Validate(ApprovalPolicy policy)
        {
            var problems = new List<string>();

            if (policy is null)
            {
                problems.Add("The policy is missing.");
                return problems;
            }

            if (string.IsNullOrWhiteSpace(policy.Name))
            {
                problems.Add("The policy needs a name.");
            }
            else if (ModelHub.GetApprovalPolicies(policy.WorkspaceId).Any(x => x.Id != policy.Id && string.Equals(x.Name, policy.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"The workspace already has a policy named '{policy.Name}'.");
            }

            var @class = CoreHub.ClassManager.GetClass(policy.ClassId);

            if (@class is null || @class.WorkspaceId != policy.WorkspaceId)
            {
                problems.Add("The policy needs a class of its workspace.");
            }
            else
            {
                var statuses = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(@class.Id)).Select(x => x.Id).ToHashSet();

                foreach (var (state, label) in new[] { (policy.RequestStatusId, "request"), (policy.ApprovedStatusId, "approved"), (policy.RejectedStatusId, "rejected") })
                {
                    if (state is Guid id && !statuses.Contains(id))
                    {
                        problems.Add($"The {label} state does not belong to the class's workflow.");
                    }
                }

                // reaching the request state again on approval or rejection would request anew
                if (policy.RequestStatusId is not null && (policy.RequestStatusId == policy.ApprovedStatusId || policy.RequestStatusId == policy.RejectedStatusId))
                {
                    problems.Add("The request state cannot be the state an approval ends in.");
                }
            }

            problems.AddRange(ApprovalSteps.Validate(policy.Steps));

            return problems;
        }

        /// <inheritdoc/>
        public IApprovalManager AddPolicy(ApprovalPolicy policy)
        {
            Require(policy);

            policy.Id = policy.Id == Guid.Empty ? Guid.NewGuid() : policy.Id;
            policy.Name = policy.Name.Trim();
            policy.Created = policy.Created == default ? DateTime.UtcNow : policy.Created;
            policy.Updated = DateTime.UtcNow;

            ModelHub.AddApprovalPolicy(policy);
            PolicyAdded?.Invoke(this, policy);

            return this;
        }

        /// <inheritdoc/>
        public IApprovalManager UpdatePolicy(ApprovalPolicy policy)
        {
            Require(policy);

            policy.Name = policy.Name.Trim();
            policy.Updated = DateTime.UtcNow;

            ModelHub.UpdateApprovalPolicy(policy);
            PolicyUpdated?.Invoke(this, policy);

            return this;
        }

        /// <inheritdoc/>
        public IApprovalManager RemovePolicy(Guid policyId)
        {
            var policy = ModelHub.GetApprovalPolicy(policyId);

            if (policy is null)
            {
                return this;
            }

            ModelHub.RemoveApprovalPolicy(policyId);
            PolicyRemoved?.Invoke(this, policy);

            return this;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Approval> GetApprovals(Guid objectId)
        {
            return ModelHub.GetApprovals(objectId);
        }

        /// <inheritdoc/>
        public Approval GetApproval(Guid approvalId)
        {
            return ModelHub.GetApproval(approvalId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<ApprovalStep> StepsOf(Approval approval)
        {
            return ApprovalSteps.Of(approval);
        }

        /// <inheritdoc/>
        public Approval Request(Guid objectId, Guid policyId, Guid requesterId, string note)
        {
            var policy = ModelHub.GetApprovalPolicy(policyId);

            if (policy is not { State: ApprovalPolicyState.Active })
            {
                throw new ApprovalRefusedException("The approval policy is not in use.");
            }

            ObjectEntity @object;

            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                @object = CoreHub.ObjectManager.GetObject(objectId);
            }

            if (@object is null || @object.ClassId != policy.ClassId)
            {
                throw new ApprovalRefusedException("The policy does not approve objects of this class.");
            }

            if (ModelHub.GetApprovals(objectId).Any(x => x.PolicyId == policy.Id && x.State == ApprovalState.Pending))
            {
                throw new ApprovalRefusedException("An approval under this policy is already pending.");
            }

            var steps = ApprovalSteps.Resolve(policy.Steps, @object);
            var first = NextApplying(steps, 0);
            var now = DateTime.UtcNow;
            var approval = new Approval
            {
                Id = Guid.NewGuid(),
                ObjectId = objectId,
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                Steps = ApprovalSteps.Serialize(steps),
                AllowSelfApproval = policy.AllowSelfApproval,
                State = first < steps.Count ? ApprovalState.Pending : ApprovalState.Approved,
                CurrentStep = first,
                RequestedById = requesterId,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Requested = now,
                StepStarted = now,
                Completed = first < steps.Count ? null : now,
                Version = Guid.NewGuid()
            };

            ModelHub.AddApproval(approval);
            ApprovalRequested?.Invoke(this, approval);

            CoreHub.AutomationManager?.Raise(new AutomationEvent
            {
                Trigger = AutomationTriggerCatalog.ApprovalRequested,
                ObjectId = objectId,
                Subjects = [policy.Name],
                Occurrence = $"approval:{approval.Id:N}:requested",
                ActorId = requesterId
            });

            if (approval.State == ApprovalState.Pending)
            {
                NotifyApprovers(approval, steps[first], @object);
            }
            else
            {
                // no step applies to this object: there is nobody to ask, and nothing to wait for
                Conclude(approval, requesterId);
            }

            return ModelHub.GetApproval(approval.Id);
        }

        /// <inheritdoc/>
        public Approval Decide(Guid approvalId, Guid identityId, ApprovalVerdict verdict, string reason)
        {
            if (verdict == ApprovalVerdict.Reject && string.IsNullOrWhiteSpace(reason))
            {
                throw new ApprovalRefusedException("A rejection needs a reason.");
            }

            // a concurrent decision on the same approval makes the write fail; the decision is
            // then taken again on the state the other one left
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var approval = ModelHub.GetApproval(approvalId)
                    ?? throw new ApprovalRefusedException("The approval does not exist.");

                if (approval.State != ApprovalState.Pending)
                {
                    throw new ApprovalRefusedException("The approval has already been decided.");
                }

                if (!IsApprover(approval, identityId))
                {
                    throw new ApprovalRefusedException("You do not decide this step of the approval.", forbidden: true);
                }

                var steps = ApprovalSteps.Of(approval);
                var step = steps[approval.CurrentStep];
                var now = DateTime.UtcNow;
                var decision = new ApprovalDecision
                {
                    Id = Guid.NewGuid(),
                    ApprovalId = approval.Id,
                    Step = approval.CurrentStep,
                    IdentityId = identityId,
                    Verdict = verdict,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                    Decided = now
                };
                var advanced = false;

                if (verdict == ApprovalVerdict.Reject)
                {
                    approval.State = ApprovalState.Rejected;
                    approval.Completed = now;
                }
                else if (IsComplete(step, approval.CurrentStep, [.. approval.Decisions, decision]))
                {
                    var next = NextApplying(steps, approval.CurrentStep + 1);

                    if (next >= steps.Count)
                    {
                        approval.State = ApprovalState.Approved;
                        approval.Completed = now;
                    }
                    else
                    {
                        approval.CurrentStep = next;
                        approval.StepStarted = now;
                        advanced = true;
                    }
                }

                if (!ModelHub.UpdateApproval(approval, decision))
                {
                    continue;
                }

                ApprovalDecided?.Invoke(this, decision);

                if (advanced)
                {
                    NotifyApprovers(approval, steps[approval.CurrentStep], ReadObject(approval.ObjectId));
                }

                if (approval.State != ApprovalState.Pending)
                {
                    Conclude(approval, identityId);
                }

                return ModelHub.GetApproval(approval.Id);
            }

            throw new ApprovalRefusedException("The approval is being decided by somebody else; try again.");
        }

        /// <inheritdoc/>
        public Approval Cancel(Guid approvalId, Guid identityId, bool administrator)
        {
            var approval = ModelHub.GetApproval(approvalId)
                ?? throw new ApprovalRefusedException("The approval does not exist.");

            if (approval.State != ApprovalState.Pending)
            {
                throw new ApprovalRefusedException("The approval has already been decided.");
            }

            if (!administrator && (identityId == Guid.Empty || identityId != approval.RequestedById))
            {
                throw new ApprovalRefusedException("Only the requester or an administrator withdraws an approval.", forbidden: true);
            }

            approval.State = ApprovalState.Cancelled;
            approval.Completed = DateTime.UtcNow;

            if (!ModelHub.UpdateApproval(approval, null))
            {
                throw new ApprovalRefusedException("The approval is being decided by somebody else; try again.");
            }

            Conclude(approval, identityId);

            return ModelHub.GetApproval(approval.Id);
        }

        /// <inheritdoc/>
        public bool IsApprover(Approval approval, Guid identityId)
        {
            if (approval is not { State: ApprovalState.Pending } || identityId == Guid.Empty)
            {
                return false;
            }

            if (!approval.AllowSelfApproval && identityId == approval.RequestedById)
            {
                return false;
            }

            var steps = ApprovalSteps.Of(approval);

            if (approval.CurrentStep < 0 || approval.CurrentStep >= steps.Count)
            {
                return false;
            }

            if (approval.Decisions.Any(x => x.Step == approval.CurrentStep && x.IdentityId == identityId))
            {
                return false;
            }

            var step = steps[approval.CurrentStep];

            return step.Identities.Contains(identityId) || GroupsOf(identityId).Overlaps(step.Groups);
        }

        /// <inheritdoc/>
        public bool IsGranted(Guid objectId)
        {
            return ModelHub.GetApprovals(objectId)
                .Where(x => x.State != ApprovalState.Cancelled)
                .FirstOrDefault()?.State == ApprovalState.Approved;
        }

        /// <inheritdoc/>
        public int RaiseOverdue(DateTime now)
        {
            var overdue = 0;

            foreach (var approval in ModelHub.GetApprovals(ApprovalState.Pending, 1000))
            {
                var steps = ApprovalSteps.Of(approval);

                if (approval.CurrentStep >= steps.Count || steps[approval.CurrentStep].DueHours is not int hours
                    || approval.StepStarted.AddHours(hours) > now)
                {
                    continue;
                }

                overdue++;

                // once per step: the occurrence names the step, so the next check queues nothing
                CoreHub.AutomationManager?.Raise(new AutomationEvent
                {
                    Trigger = AutomationTriggerCatalog.ApprovalOverdue,
                    ObjectId = approval.ObjectId,
                    Subjects = [steps[approval.CurrentStep].Name, approval.PolicyName],
                    Occurrence = $"approval:{approval.Id:N}:overdue:{approval.CurrentStep}"
                });
            }

            return overdue;
        }

        /// <inheritdoc/>
        public void Connect()
        {
            if (_connected)
            {
                return;
            }

            _connected = true;

            CoreHub.WorkflowManager.TransitionExecuted += (_, result) => OnTransition(result);
        }

        /// <summary>
        /// Requests the approvals whose request state the object just reached.
        /// </summary>
        /// <param name="result">The move.</param>
        private void OnTransition(WorkflowTransitionResult result)
        {
            if (result?.Outcome != WorkflowTransitionOutcome.Executed || result.Target is null)
            {
                return;
            }

            var @object = ReadObject(result.ObjectId);

            if (@object is null)
            {
                return;
            }

            foreach (var policy in ModelHub.GetApprovalPolicies(@object.WorkspaceId)
                .Where(x => x.State == ApprovalPolicyState.Active && x.ClassId == @object.ClassId && x.RequestStatusId == result.Target.Id))
            {
                try
                {
                    Request(@object.Id, policy.Id, result.IdentityId != Guid.Empty ? result.IdentityId : CoreHub.SessionManager.GetCurrentIdentityId(null), null);
                }
                catch (ApprovalRefusedException)
                {
                    // one is already pending - reaching the state again does not ask twice
                }
            }
        }

        /// <summary>
        /// Ends an approval: tells the requester, lets rules react, and moves the object to the
        /// state the policy names for the outcome.
        /// </summary>
        /// <param name="approval">The decided approval.</param>
        /// <param name="actorId">Who decided last, on whose behalf the object is moved.</param>
        private void Conclude(Approval approval, Guid actorId)
        {
            ApprovalCompleted?.Invoke(this, approval);

            var outcome = approval.State.ToString().ToLowerInvariant();

            CoreHub.AutomationManager?.Raise(new AutomationEvent
            {
                Trigger = AutomationTriggerCatalog.ApprovalCompleted,
                ObjectId = approval.ObjectId,
                Subjects = [outcome, approval.PolicyName],
                Occurrence = $"approval:{approval.Id:N}:completed",
                ActorId = actorId
            });

            var @object = ReadObject(approval.ObjectId);

            if (@object is null)
            {
                return;
            }

            if (approval.RequestedById != Guid.Empty && approval.RequestedById != actorId)
            {
                Notify(approval.RequestedById, $"kleenestar.core:approval.notification.{outcome}", @object);
            }

            var policy = ModelHub.GetApprovalPolicy(approval.PolicyId);
            var target = approval.State switch
            {
                ApprovalState.Approved => policy?.ApprovedStatusId,
                ApprovalState.Rejected => policy?.RejectedStatusId,
                _ => null
            };

            if (target is not Guid statusId)
            {
                return;
            }

            var message = Move(@object, statusId, actorId);

            if (message is not null)
            {
                approval.Outcome = message;
                ModelHub.UpdateApproval(approval, null);
            }
        }

        /// <summary>
        /// Moves an object through its workflow.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="statusId">The target state.</param>
        /// <param name="actorId">On whose behalf.</param>
        /// <returns>Why the move did not happen, or <see langword="null"/> when it did.</returns>
        private static string Move(ObjectEntity @object, Guid statusId, Guid actorId)
        {
            var field = StandardActions.FieldsOf(@object.ClassId).FirstOrDefault(x => x.FieldType == FieldType.Workflow);

            if (field is null)
            {
                return "The class has no workflow field.";
            }

            try
            {
                using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

                var result = CoreHub.WorkflowManager.ExecuteTransition(@object.Id, field.Id, statusId, actorId);

                return result?.Outcome == WorkflowTransitionOutcome.Executed
                    ? null
                    : $"The move after the decision was refused: {result?.Outcome} {result?.Message}".Trim();
            }
            catch (Exception ex)
            {
                return $"The move after the decision failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Tells the approvers of a step that it awaits them.
        /// </summary>
        /// <param name="approval">The approval.</param>
        /// <param name="step">The step.</param>
        /// <param name="object">The object.</param>
        private static void NotifyApprovers(Approval approval, ApprovalStep step, ObjectEntity @object)
        {
            var recipients = new HashSet<Guid>(step.Identities);

            foreach (var groupId in step.Groups)
            {
                foreach (var membership in CoreHub.GroupManager.GetGroup(groupId)?.GroupMemberships ?? [])
                {
                    if (membership.Identity?.Id is Guid memberId)
                    {
                        recipients.Add(memberId);
                    }
                }
            }

            if (!approval.AllowSelfApproval)
            {
                recipients.Remove(approval.RequestedById);
            }

            foreach (var recipient in recipients)
            {
                Notify(recipient, "kleenestar.core:approval.notification.pending", @object);
            }
        }

        /// <summary>
        /// Puts a notification about an object into an account's notification center.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <param name="messageKey">The i18n key of the message.</param>
        /// <param name="object">The object.</param>
        private static void Notify(Guid identityId, string messageKey, ObjectEntity @object)
        {
            var subject = NotificationSubject.Describe(@object);

            // the notification center addresses the identity acting on the call chain
            using var addressed = CoreHub.SessionManager.BeginIdentity(identityId);

            CoreHub.NotificationCenterManager?.Record("kleenestar.core:approval.notification.title", messageKey, subject?.Label, subject?.TargetUri, subject?.IconUri);
        }

        /// <summary>
        /// Determines whether a step has enough approvals.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="index">The step's index.</param>
        /// <param name="decisions">The decisions of the approval.</param>
        /// <returns><see langword="true"/> when the step is approved.</returns>
        private static bool IsComplete(ApprovalStep step, int index, IEnumerable<ApprovalDecision> decisions)
        {
            var approvers = decisions
                .Where(x => x.Step == index && x.Verdict == ApprovalVerdict.Approve)
                .Select(x => x.IdentityId)
                .Distinct()
                .ToList();

            return step.Mode switch
            {
                ApprovalMode.Any => approvers.Count >= 1,
                ApprovalMode.Quorum => approvers.Count >= Math.Max(1, step.Quorum),
                _ => step.Identities.All(approvers.Contains)
                    && step.Groups.All(group => approvers.Any(x => GroupsOf(x).Contains(group)))
            };
        }

        /// <summary>
        /// Returns the index of the next step that applies.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <param name="from">The index to start at.</param>
        /// <returns>The index, or the number of steps when none is left.</returns>
        private static int NextApplying(IReadOnlyList<ApprovalStep> steps, int from)
        {
            var index = from;

            while (index < steps.Count && !steps[index].Applies)
            {
                index++;
            }

            return index;
        }

        /// <summary>
        /// Returns the groups an account is a member of.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <returns>The group ids.</returns>
        private static HashSet<Guid> GroupsOf(Guid identityId)
        {
            var identity = CoreHub.IdentityManager.GetIdentity(identityId);

            return [.. (identity?.GroupMemberships ?? []).Where(x => x.Group is not null).Select(x => x.Group.Id)];
        }

        /// <summary>
        /// Reads an object whatever its classification: approvals are kept about it, not shown.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ReadObject(Guid objectId)
        {
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            return CoreHub.ObjectManager.GetObject(objectId);
        }

        /// <summary>
        /// Refuses a policy that is not valid.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <exception cref="ArgumentException">The policy is not valid.</exception>
        private void Require(ApprovalPolicy policy)
        {
            var problems = Validate(policy);

            if (problems.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", problems), nameof(policy));
            }
        }

        /// <summary>
        /// Releases the resources of the manager.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
