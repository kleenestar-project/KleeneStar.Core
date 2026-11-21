using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// An approval policy a template sets up for one of its classes.
    /// </summary>
    /// <remarks>
    /// The texts may be i18n keys. The steps follow the policy syntax
    /// (<c>kleenestar.approval.md</c>) and may carry two kinds of placeholder: <c>{creator}</c>
    /// for the user name of whoever creates the workspace - the one approver a new workspace is
    /// sure to have, replaced by the real ones afterwards - and <c>{namespace:key}</c> for a
    /// translated text, so step names arrive in the creator's language. The states are named
    /// as the class's workflow declares them.
    /// </remarks>
    public sealed class WorkspaceTemplateApproval
    {
        /// <summary>Gets the name.</summary>
        public string Name { get; init; }

        /// <summary>Gets the description.</summary>
        public string Description { get; init; }

        /// <summary>Gets the steps, one per line.</summary>
        public string Steps { get; init; }

        /// <summary>Gets the state whose reaching requests the approval, or <c>null</c>.</summary>
        public string RequestState { get; init; }

        /// <summary>Gets the state an approved object moves to, or <c>null</c>.</summary>
        public string ApprovedState { get; init; }

        /// <summary>Gets the state a rejected object moves to, or <c>null</c>.</summary>
        public string RejectedState { get; init; }

        /// <summary>Gets whether the requester may decide too.</summary>
        public bool AllowSelfApproval { get; init; }
    }

    /// <summary>
    /// The processes a template set up.
    /// </summary>
    public sealed class WorkspaceTemplateProcessResult
    {
        /// <summary>Gets the approval policies created.</summary>
        public List<Model.Entities.ApprovalPolicy> ApprovalPolicies { get; } = [];

        /// <summary>Gets what could not be set up, and why.</summary>
        public List<string> Skipped { get; } = [];
    }
}
