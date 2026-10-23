using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Reports the outcome of a state change requested on a workflow-backed field of an
    /// object, as executed by <see cref="IWorkflowManager.ExecuteTransition"/>.
    /// </summary>
    /// <remarks>
    /// The state change runs the stages the workflow concept prescribes — guard, validator,
    /// apply, post function (see <c>KleeneStar.Core/docs/kleenestar.workflow.md</c>) — and the
    /// result says which stage stopped it. <see cref="Outcome"/> is the machine-readable
    /// verdict; <see cref="Message"/> is the internationalization key the caller surfaces to
    /// the user; <see cref="ValidationErrors"/> carries the individual findings when the
    /// validator stage rejected the change.
    /// </remarks>
    public sealed class WorkflowTransitionResult
    {
        /// <summary>
        /// Gets a value indicating whether the state change was applied.
        /// </summary>
        public bool Succeeded => Outcome == WorkflowTransitionOutcome.Executed;

        /// <summary>
        /// Gets the verdict of the state change.
        /// </summary>
        public WorkflowTransitionOutcome Outcome { get; init; }

        /// <summary>
        /// Gets the internationalization key describing the outcome to the user.
        /// </summary>
        public string Message { get; init; }

        /// <summary>
        /// Gets the id of the object whose state was changed.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets the id of the workflow-backed field that carries the state.
        /// </summary>
        public Guid FieldId { get; init; }

        /// <summary>
        /// Gets on whose behalf the change was made, or <see cref="Guid.Empty"/> when unknown -
        /// whoever reacts to the change (an approval it requests) learns who asked.
        /// </summary>
        public Guid IdentityId { get; init; }

        /// <summary>
        /// Gets the state the object was in before the change, or <c>null</c> when the object
        /// carried no resolvable state and the change entered the workflow.
        /// </summary>
        public Status Source { get; init; }

        /// <summary>
        /// Gets the state that was requested, or <c>null</c> when it could not be resolved.
        /// </summary>
        public Status Target { get; init; }

        /// <summary>
        /// Gets the transition that carried the change, or <c>null</c> when the object entered
        /// the workflow at an entry state and therefore travelled along no transition.
        /// </summary>
        public Transition Transition { get; init; }

        /// <summary>
        /// Gets the findings of the validator stage. Empty unless <see cref="Outcome"/> is
        /// <see cref="WorkflowTransitionOutcome.ValidationFailed"/>.
        /// </summary>
        public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    }

    /// <summary>
    /// The verdicts <see cref="IWorkflowManager.ExecuteTransition"/> can reach.
    /// </summary>
    public enum WorkflowTransitionOutcome
    {
        /// <summary>
        /// The state change was applied and the post functions ran.
        /// </summary>
        Executed,

        /// <summary>
        /// The object, the field, the workflow or the requested state could not be resolved.
        /// </summary>
        NotFound,

        /// <summary>
        /// The object already carries the requested state, so there is nothing to change.
        /// </summary>
        Unchanged,

        /// <summary>
        /// No active transition connects the object's current state to the requested one, or
        /// the workflow is not published.
        /// </summary>
        NotAllowed,

        /// <summary>
        /// A validator of the transition rejected the change. The findings are carried by
        /// <see cref="WorkflowTransitionResult.ValidationErrors"/>.
        /// </summary>
        ValidationFailed,

        /// <summary>
        /// A relation of the object refuses the move: something that blocks it is still open.
        /// The blocking objects are named by
        /// <see cref="WorkflowTransitionResult.ValidationErrors"/>.
        /// </summary>
        Blocked
    }
}
