using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebApproval
{
    /// <summary>
    /// What happened to an approval.
    /// </summary>
    public enum ApprovalOccurrenceKind
    {
        /// <summary>The approval was requested.</summary>
        Requested,

        /// <summary>The approval ended: approved, rejected or withdrawn.</summary>
        Completed,

        /// <summary>The current step ran past its deadline.</summary>
        Overdue
    }

    /// <summary>
    /// An occurrence in the life of an approval, raised by
    /// <see cref="WebManager.IApprovalManager.ApprovalOccurred"/> for whoever reacts to approvals
    /// - the automation plugin turns it into its <c>approval.*</c> triggers. The core does not know
    /// who listens.
    /// </summary>
    /// <param name="Kind">What happened.</param>
    /// <param name="Approval">The approval, as it is after the occurrence.</param>
    /// <param name="ActorId">Who caused it - the requester, the last approver; empty for a deadline.</param>
    /// <param name="StepName">The step it concerns (for <see cref="ApprovalOccurrenceKind.Overdue"/>), or <see langword="null"/>.</param>
    public sealed record ApprovalOccurrence(ApprovalOccurrenceKind Kind, Approval Approval, Guid ActorId, string StepName = null);
}
