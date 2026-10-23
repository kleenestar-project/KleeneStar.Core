using KleeneStar.Core.WebApproval;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Multi-step approvals: the policies of the workspaces, and the approvals of objects that
    /// follow them.
    /// </summary>
    public interface IApprovalManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a policy is added.
        /// </summary>
        event EventHandler<ApprovalPolicy> PolicyAdded;

        /// <summary>
        /// An event that fires when a policy is changed.
        /// </summary>
        event EventHandler<ApprovalPolicy> PolicyUpdated;

        /// <summary>
        /// An event that fires when a policy is removed.
        /// </summary>
        event EventHandler<ApprovalPolicy> PolicyRemoved;

        /// <summary>
        /// An event that fires when an approval is requested.
        /// </summary>
        event EventHandler<Approval> ApprovalRequested;

        /// <summary>
        /// An event that fires when an approver decided.
        /// </summary>
        event EventHandler<ApprovalDecision> ApprovalDecided;

        /// <summary>
        /// An event that fires when an approval ended: approved, rejected or withdrawn.
        /// </summary>
        event EventHandler<Approval> ApprovalCompleted;

        /// <summary>
        /// Returns the policies of a workspace, or of every workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace, or <see langword="null"/>.</param>
        /// <returns>The policies.</returns>
        IReadOnlyList<ApprovalPolicy> GetPolicies(Guid? workspaceId);

        /// <summary>
        /// Returns a policy.
        /// </summary>
        /// <param name="policyId">The policy.</param>
        /// <returns>The policy, or <see langword="null"/>.</returns>
        ApprovalPolicy GetPolicy(Guid policyId);

        /// <summary>
        /// Checks a policy before it is stored.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The problems; empty when the policy may be stored.</returns>
        IReadOnlyList<string> Validate(ApprovalPolicy policy);

        /// <summary>
        /// Adds a policy.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The manager, for chaining.</returns>
        /// <exception cref="ArgumentException">The policy is not valid.</exception>
        IApprovalManager AddPolicy(ApprovalPolicy policy);

        /// <summary>
        /// Writes the changes of a policy; running approvals keep the steps they were requested with.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The manager, for chaining.</returns>
        /// <exception cref="ArgumentException">The policy is not valid.</exception>
        IApprovalManager UpdatePolicy(ApprovalPolicy policy);

        /// <summary>
        /// Removes a policy; the approvals that followed it stay.
        /// </summary>
        /// <param name="policyId">The policy.</param>
        /// <returns>The manager, for chaining.</returns>
        IApprovalManager RemovePolicy(Guid policyId);

        /// <summary>
        /// Returns the approvals of an object, the newest first.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The approvals with their decisions.</returns>
        IReadOnlyList<Approval> GetApprovals(Guid objectId);

        /// <summary>
        /// Returns an approval.
        /// </summary>
        /// <param name="approvalId">The approval.</param>
        /// <returns>The approval with its decisions, or <see langword="null"/>.</returns>
        Approval GetApproval(Guid approvalId);

        /// <summary>
        /// Requests an approval of an object under a policy.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="policyId">The policy.</param>
        /// <param name="requesterId">Who asks; empty for a rule or the workflow.</param>
        /// <param name="note">What the requester says, or <see langword="null"/>.</param>
        /// <returns>The approval - already approved when no step applies.</returns>
        /// <exception cref="ApprovalRefusedException">The approval cannot be requested.</exception>
        Approval Request(Guid objectId, Guid policyId, Guid requesterId, string note);

        /// <summary>
        /// Records the decision of an approver on the step awaiting decisions.
        /// </summary>
        /// <param name="approvalId">The approval.</param>
        /// <param name="identityId">The approver.</param>
        /// <param name="verdict">The verdict.</param>
        /// <param name="reason">The reason; required for a rejection.</param>
        /// <returns>The approval after the decision.</returns>
        /// <exception cref="ApprovalRefusedException">The decision is not possible.</exception>
        Approval Decide(Guid approvalId, Guid identityId, ApprovalVerdict verdict, string reason);

        /// <summary>
        /// Withdraws a pending approval.
        /// </summary>
        /// <param name="approvalId">The approval.</param>
        /// <param name="identityId">Who withdraws it.</param>
        /// <param name="administrator">Whether that one administers the object's workspace;
        /// otherwise only the requester may withdraw.</param>
        /// <returns>The approval.</returns>
        /// <exception cref="ApprovalRefusedException">The approval cannot be withdrawn.</exception>
        Approval Cancel(Guid approvalId, Guid identityId, bool administrator);

        /// <summary>
        /// Returns the steps an approval was requested with.
        /// </summary>
        /// <param name="approval">The approval.</param>
        /// <returns>The steps.</returns>
        IReadOnlyList<ApprovalStep> StepsOf(Approval approval);

        /// <summary>
        /// Determines whether an account may decide the step an approval awaits.
        /// </summary>
        /// <param name="approval">The approval.</param>
        /// <param name="identityId">The account.</param>
        /// <returns><see langword="true"/> for an approver of the step who has not decided it yet.</returns>
        bool IsApprover(Approval approval, Guid identityId);

        /// <summary>
        /// Determines whether an object's latest approval is approved - what the workflow
        /// validator <c>approval.granted</c> asks.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns><see langword="true"/> when approved.</returns>
        bool IsGranted(Guid objectId);

        /// <summary>
        /// Raises <c>approval.overdue</c> for every step that took longer than it may, once per step.
        /// </summary>
        /// <param name="now">The point in time.</param>
        /// <returns>The overdue steps found.</returns>
        int RaiseOverdue(DateTime now);

        /// <summary>
        /// Subscribes to the workflow, so a policy's request state requests its approval.
        /// </summary>
        void Connect();
    }

    /// <summary>
    /// An approval action that is not possible, with the reason the caller is meant to read.
    /// </summary>
    /// <param name="message">The reason.</param>
    /// <param name="forbidden">Whether the caller lacks the right rather than the request being wrong.</param>
    public sealed class ApprovalRefusedException(string message, bool forbidden = false) : InvalidOperationException(message)
    {
        /// <summary>
        /// Gets whether the caller lacks the right rather than the request being wrong.
        /// </summary>
        public bool Forbidden { get; } = forbidden;
    }
}
