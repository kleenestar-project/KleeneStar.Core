using KleeneStar.Core.WebManager;
using KleeneStar.Core.WWW.Api._1_.Automation;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Approvals
{
    /// <summary>
    /// REST endpoint recording a decision: <c>POST /api/1/approvals/decide</c> with
    /// <c>{ "approval", "verdict": "approve" | "reject", "reason" }</c>.
    /// </summary>
    /// <remarks>
    /// Only an approver of the step the approval awaits decides, once; a rejection needs a
    /// reason. The decision is recorded with the caller and the time.
    /// </remarks>
    [Title("kleenestar.core:approval.api.decide.title")]
    [Cache]
    public sealed class Decide : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Decide()
        {
        }

        /// <summary>
        /// Handles <c>POST</c>: the decision.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The approval after the decision, or why it was not taken.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Record(IRequest request)
        {
            var payload = AutomationApi.Read<ApprovalApi.Payload>(request);
            var approval = ApprovalApi.Resolve(payload, out _);

            if (approval is null)
            {
                return new ResponseNotFound();
            }

            var verdict = payload.Verdict?.Trim().ToLowerInvariant() switch
            {
                "approve" => ApprovalVerdict.Approve,
                "reject" => ApprovalVerdict.Reject,
                _ => (ApprovalVerdict?)null
            };

            if (verdict is null)
            {
                return AutomationApi.Refuse("Choose to approve or to reject.");
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            try
            {
                return AutomationApi.Json(ApprovalApi.ToDto(CoreHub.ApprovalManager.Decide(approval.Id, callerId, verdict.Value, payload.Reason), callerId));
            }
            catch (ApprovalRefusedException refusal)
            {
                return ApprovalApi.Refuse(refusal);
            }
        }
    }
}
