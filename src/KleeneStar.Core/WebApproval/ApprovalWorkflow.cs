using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebApproval
{
    /// <summary>
    /// The object has to be approved: its latest approval, withdrawn ones aside, ended approved.
    /// </summary>
    /// <remarks>
    /// The validator a transition gets when it may only be taken after an approval - the move
    /// into implementation of a change, the release of a deployment. It complements the policy's
    /// approved state, which moves the object on its own; with the validator, a person moves it
    /// and the workflow checks.
    /// </remarks>
    public sealed class ApprovalGrantedValidator : IWorkflowValidator
    {
        /// <inheritdoc/>
        public string Key => "approval.granted";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:workflow.rule.approval.granted.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:workflow.rule.approval.granted.description";

        /// <inheritdoc/>
        public string Message => "kleenestar.core:workflow.rule.approval.granted.message";

        /// <inheritdoc/>
        public IIcon Icon => new IconStamp();

        /// <inheritdoc/>
        public int Order => 60;

        /// <inheritdoc/>
        public bool Evaluate(WorkflowRuleContext context)
        {
            return context?.Object is { } @object && CoreHub.ApprovalManager.IsGranted(@object.Id);
        }
    }

    /// <summary>
    /// <c>approval &lt;policy&gt;</c> - requests an approval of the object under a policy of the
    /// rule's workspace.
    /// </summary>
    /// <remarks>
    /// An approval already pending under the policy counts as done, which also makes a resumed
    /// job request nothing twice.
    /// </remarks>
    internal sealed class ApprovalAction : IAutomationAction
    {
        /// <inheritdoc/>
        public string Key => "approval";

        /// <inheritdoc/>
        public string Syntax => "kleenestar.core:automation.action.approval";

        /// <inheritdoc/>
        public string Validate(string argument, Class @class)
        {
            return string.IsNullOrWhiteSpace(argument) ? "name the approval policy." : null;
        }

        /// <inheritdoc/>
        public string Validate(string argument, Class @class, Guid workspaceId)
        {
            if (Validate(argument, @class) is { } problem || workspaceId == Guid.Empty)
            {
                return Validate(argument, @class);
            }

            var policy = Find(workspaceId, argument);

            if (policy is null)
            {
                return $"the workspace has no approval policy '{argument.Trim()}'.";
            }

            return @class is not null && policy.ClassId != @class.Id
                ? "the policy approves objects of another class."
                : null;
        }

        /// <inheritdoc/>
        public void Execute(AutomationActionContext context, string argument)
        {
            var policy = Find(context.Rule.WorkspaceId, argument)
                ?? throw new InvalidOperationException($"The workspace has no approval policy '{argument?.Trim()}'.");

            if (CoreHub.ApprovalManager.GetApprovals(context.Object.Id).Any(x => x.PolicyId == policy.Id && x.State == ApprovalState.Pending))
            {
                return;
            }

            CoreHub.ApprovalManager.Request(context.Object.Id, policy.Id, context.ActorId, null);
        }

        /// <summary>
        /// Finds a policy of a workspace by name.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="name">The name.</param>
        /// <returns>The policy, or <see langword="null"/>.</returns>
        private static ApprovalPolicy Find(Guid workspaceId, string name)
        {
            return CoreHub.ApprovalManager
                .GetPolicies(workspaceId)
                .FirstOrDefault(x => string.Equals(x.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
