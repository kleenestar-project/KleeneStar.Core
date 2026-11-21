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
}
