using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WWW.Api._1_.Automation;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.ApprovalPolicies._workspacekey_
{
    /// <summary>
    /// REST endpoint over the approval policies of a workspace: <c>GET</c> lists them,
    /// <c>POST</c> adds one or - with an <c>id</c> - changes one. The URL is
    /// <c>/api/1/approvalpolicies/{workspacekey}</c>.
    /// </summary>
    /// <remarks>
    /// Who approves what is workspace administration. A policy is validated in full - every
    /// step, approver and condition, every state against the class's workflow - and refused
    /// with the problems.
    /// </remarks>
    [Title("kleenestar.core:approval.api.policies.title")]
    [WorkspaceKeySegment]
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
        /// Handles <c>GET</c>: the policies of the workspace.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The policies as JSON.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            return AutomationApi.Json(CoreHub.ApprovalManager.GetPolicies(workspace.Id).Select(ToDto));
        }

        /// <summary>
        /// Handles <c>POST</c>: adds a policy, or changes the one the body names.
        /// </summary>
        /// <param name="request">The request carrying the policy.</param>
        /// <returns>The stored policy, or the problems that kept it from being stored.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var payload = AutomationApi.Read<PolicyPayload>(request);

            if (payload is null)
            {
                return AutomationApi.Refuse("The request body must describe an approval policy.");
            }

            var id = AutomationApi.ParseId(payload.Id);
            var existing = id is { } policyId ? CoreHub.ApprovalManager.GetPolicy(policyId) : null;

            if (id is not null && (existing is null || existing.WorkspaceId != workspace.Id))
            {
                return new ResponseNotFound();
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var policy = existing ?? new ApprovalPolicy
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                CreatedById = callerId == Guid.Empty ? null : callerId
            };

            policy.Name = payload.Name?.Trim();
            policy.Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim();
            policy.ClassId = AutomationApi.ParseId(payload.ClassId) ?? Guid.Empty;
            policy.Steps = payload.Steps?.Replace("\r\n", "\n");
            policy.State = string.Equals(payload.State, "disabled", StringComparison.OrdinalIgnoreCase)
                ? ApprovalPolicyState.Disabled
                : ApprovalPolicyState.Active;
            policy.AllowSelfApproval = string.Equals(payload.AllowSelfApproval, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(payload.AllowSelfApproval, "on", StringComparison.OrdinalIgnoreCase);
            policy.RequestStatusId = AutomationApi.ParseId(payload.RequestStatusId);
            policy.ApprovedStatusId = AutomationApi.ParseId(payload.ApprovedStatusId);
            policy.RejectedStatusId = AutomationApi.ParseId(payload.RejectedStatusId);

            var problems = CoreHub.ApprovalManager.Validate(policy);

            if (problems.Count > 0)
            {
                return AutomationApi.Refuse(string.Join(" ", problems));
            }

            if (existing is null)
            {
                CoreHub.ApprovalManager.AddPolicy(policy);
            }
            else
            {
                CoreHub.ApprovalManager.UpdatePolicy(policy);
            }

            return AutomationApi.Json(ToDto(CoreHub.ApprovalManager.GetPolicy(policy.Id)));
        }

        /// <summary>
        /// Projects a policy onto its wire shape.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <returns>The DTO.</returns>
        internal static object ToDto(ApprovalPolicy policy)
        {
            return new
            {
                id = policy.Id,
                name = policy.Name,
                description = policy.Description,
                classId = policy.ClassId,
                steps = policy.Steps,
                state = policy.State.ToString().ToLowerInvariant(),
                allowSelfApproval = policy.AllowSelfApproval,
                requestStatusId = policy.RequestStatusId,
                approvedStatusId = policy.ApprovedStatusId,
                rejectedStatusId = policy.RejectedStatusId,
                updated = policy.Updated
            };
        }

        /// <summary>
        /// The body of a policy written through the page or a client. Every value is a string,
        /// because a page's form sends nothing else.
        /// </summary>
        internal sealed class PolicyPayload
        {
            /// <summary>Gets or sets the id of the policy to change, or empty for a new one.</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets the name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the description.</summary>
            public string Description { get; set; }

            /// <summary>Gets or sets the class.</summary>
            public string ClassId { get; set; }

            /// <summary>Gets or sets the steps, one per line.</summary>
            public string Steps { get; set; }

            /// <summary>Gets or sets the state, <c>active</c> or <c>disabled</c>.</summary>
            public string State { get; set; }

            /// <summary>Gets or sets whether the requester may decide too (<c>true</c>).</summary>
            public string AllowSelfApproval { get; set; }

            /// <summary>Gets or sets the state whose reaching requests the approval.</summary>
            public string RequestStatusId { get; set; }

            /// <summary>Gets or sets the state an approved object moves to.</summary>
            public string ApprovedStatusId { get; set; }

            /// <summary>Gets or sets the state a rejected object moves to.</summary>
            public string RejectedStatusId { get; set; }
        }
    }
}
