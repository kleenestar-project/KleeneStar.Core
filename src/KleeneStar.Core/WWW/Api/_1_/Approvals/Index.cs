using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Approvals
{
    /// <summary>
    /// REST endpoint over the approvals of objects: <c>GET ?object=KEY</c> lists an object's
    /// approvals with steps and decisions; <c>POST</c> requests one
    /// (<c>{ "object", "policy", "reason" }</c>). The URL is <c>/api/1/approvals</c>.
    /// </summary>
    /// <remarks>
    /// Requesting an approval is a change to the object and asks for the right to change it.
    /// </remarks>
    [Title("kleenestar.core:approval.api.title")]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET</c>: the approvals of the object the query names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The approvals, or <c>404</c> for an object the caller may not read.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request.GetParameter("object")?.Value ?? string.Empty);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return AutomationApi.Json(CoreHub.ApprovalManager.GetApprovals(@object.Id).Select(x => ApprovalApi.ToDto(x, callerId)));
        }

        /// <summary>
        /// Handles <c>POST</c>: requests an approval of an object under a policy.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The approval, or the reason it cannot be requested.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Request(IRequest request)
        {
            var payload = AutomationApi.Read<ApprovalApi.Payload>(request);
            var @object = string.IsNullOrWhiteSpace(payload?.Object) ? null : CoreHub.ObjectManager.GetObjectByKey(payload.Object);

            if (@object is null || !Guid.TryParse(payload.Policy, out var policyId))
            {
                return new ResponseNotFound();
            }

            if (!ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseForbidden();
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            try
            {
                return AutomationApi.Json(ApprovalApi.ToDto(CoreHub.ApprovalManager.Request(@object.Id, policyId, callerId, payload.Reason), callerId));
            }
            catch (ApprovalRefusedException refusal)
            {
                return ApprovalApi.Refuse(refusal);
            }
        }
    }
}
