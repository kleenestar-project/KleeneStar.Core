using KleeneStar.Core.WebManager;
using KleeneStar.Core.WWW.Api._1_.Automation;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Approvals
{
    /// <summary>
    /// Shared plumbing of the approval endpoints.
    /// </summary>
    /// <remarks>
    /// An approval is reached through its object: whoever cannot read the object (grants,
    /// clearance) learns nothing about its approvals, and the endpoints answer <c>404</c>.
    /// </remarks>
    internal static class ApprovalApi
    {
        /// <summary>
        /// The JSON body naming an approval and what to do with it.
        /// </summary>
        internal sealed class Payload
        {
            /// <summary>Gets or sets the approval.</summary>
            public string Approval { get; set; }

            /// <summary>Gets or sets the object key, for a request.</summary>
            public string Object { get; set; }

            /// <summary>Gets or sets the policy, for a request.</summary>
            public string Policy { get; set; }

            /// <summary>Gets or sets the verdict, <c>approve</c> or <c>reject</c>.</summary>
            public string Verdict { get; set; }

            /// <summary>Gets or sets the reason of a decision, or the note of a request.</summary>
            public string Reason { get; set; }
        }

        /// <summary>
        /// Resolves the approval a body names, through its object as the caller may read it.
        /// </summary>
        /// <param name="payload">The body.</param>
        /// <param name="object">The object, when the caller may read it.</param>
        /// <returns>The approval, or <see langword="null"/>.</returns>
        internal static Approval Resolve(Payload payload, out ObjectEntity @object)
        {
            @object = null;

            var approval = Guid.TryParse(payload?.Approval, out var id) ? CoreHub.ApprovalManager.GetApproval(id) : null;

            if (approval is null)
            {
                return null;
            }

            @object = CoreHub.ObjectManager.GetObject(approval.ObjectId);

            return @object is null ? null : approval;
        }

        /// <summary>
        /// Turns a refusal of the approval manager into a response the forms show.
        /// </summary>
        /// <param name="refusal">The refusal.</param>
        /// <returns><c>403</c> for a caller without the right, else <c>400</c>, each with a message.</returns>
        internal static IResponse Refuse(ApprovalRefusedException refusal)
        {
            if (!refusal.Forbidden)
            {
                return AutomationApi.Refuse(refusal.Message);
            }

            return new ResponseForbidden { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message = refusal.Message }, AutomationApi.JsonOptions)) }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Projects an approval onto its wire shape, naming the accounts.
        /// </summary>
        /// <param name="approval">The approval.</param>
        /// <param name="callerId">The caller, to tell whether they decide the current step.</param>
        /// <returns>The DTO.</returns>
        internal static object ToDto(Approval approval, Guid callerId)
        {
            var steps = CoreHub.ApprovalManager.StepsOf(approval);

            return new
            {
                id = approval.Id,
                objectId = approval.ObjectId,
                policyId = approval.PolicyId,
                policy = approval.PolicyName,
                state = approval.State.ToString().ToLowerInvariant(),
                currentStep = approval.CurrentStep,
                requestedBy = NameOf(approval.RequestedById),
                note = approval.Note,
                requested = approval.Requested,
                completed = approval.Completed,
                outcome = approval.Outcome,
                mayDecide = CoreHub.ApprovalManager.IsApprover(approval, callerId),
                steps = steps.Select((x, index) => new
                {
                    name = x.Name,
                    mode = x.Mode.ToString().ToLowerInvariant(),
                    quorum = x.Quorum,
                    approvers = x.Approvers,
                    dueHours = x.DueHours,
                    applies = x.Applies,
                    decisions = approval.Decisions
                        .Where(d => d.Step == index)
                        .OrderBy(d => d.Decided)
                        .Select(d => new
                        {
                            by = NameOf(d.IdentityId),
                            verdict = d.Verdict.ToString().ToLowerInvariant(),
                            reason = d.Reason,
                            decided = d.Decided
                        })
                })
            };
        }

        /// <summary>
        /// Returns the display name of an account.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <returns>The name, or <see langword="null"/> for nobody.</returns>
        internal static string NameOf(Guid identityId)
        {
            return identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId)?.Name;
        }
    }
}
