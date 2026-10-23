using KleeneStar.Core.WWW.Api._1_.Automation;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.ApprovalPolicies._workspacekey_
{
    /// <summary>
    /// REST endpoint removing an approval policy of a workspace. The URL is
    /// <c>/api/1/approvalpolicies/{workspacekey}/delete</c>; the body names the policy
    /// (<c>{ "id" }</c>).
    /// </summary>
    /// <remarks>
    /// The approvals that followed the policy stay with their objects - they are the record of
    /// what was decided - and a pending one can still be decided.
    /// </remarks>
    [Title("kleenestar.core:approval.api.policies.delete.title")]
    [Cache]
    public sealed class Delete : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Delete()
        {
        }

        /// <summary>
        /// Handles <c>POST</c>: removes the policy the body names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, or <c>404</c> for a policy of no workspace the route names.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Remove(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var id = AutomationApi.ParseId(AutomationApi.Read<AutomationApi.IdPayload>(request)?.Id);
            var policy = id is { } policyId ? CoreHub.ApprovalManager.GetPolicy(policyId) : null;

            if (policy is null || policy.WorkspaceId != workspace.Id)
            {
                return new ResponseNotFound();
            }

            CoreHub.ApprovalManager.RemovePolicy(policy.Id);

            return new ResponseNoContent();
        }
    }
}
