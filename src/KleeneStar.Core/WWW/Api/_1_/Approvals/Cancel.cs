using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Approvals
{
    /// <summary>
    /// REST endpoint withdrawing a pending approval: <c>POST /api/1/approvals/cancel</c> with
    /// <c>{ "approval" }</c> - by its requester, or by an administrator of the object's workspace.
    /// </summary>
    [Title("kleenestar.core:approval.api.cancel.title")]
    [Cache]
    public sealed class Cancel : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Cancel()
        {
        }

        /// <summary>
        /// Handles <c>POST</c>: withdraws the approval.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The approval, or why it cannot be withdrawn.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Withdraw(IRequest request)
        {
            var payload = AutomationApi.Read<ApprovalApi.Payload>(request);
            var approval = ApprovalApi.Resolve(payload, out var @object);

            if (approval is null)
            {
                return new ResponseNotFound();
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var administrator = ContentAuthorization.MayAdministerWorkspace(@object.WorkspaceId, request);

            try
            {
                return AutomationApi.Json(ApprovalApi.ToDto(CoreHub.ApprovalManager.Cancel(approval.Id, callerId, administrator), callerId));
            }
            catch (ApprovalRefusedException refusal)
            {
                return ApprovalApi.Refuse(refusal);
            }
        }
    }
}
