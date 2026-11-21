using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing workflows, including adding, retrieving, and removing, as well as
    /// handling workflow-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing workflows and events for tracking changes 
    /// to the workflow collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public interface IWorkflowManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an workflow is added.
        /// </summary>
        event EventHandler<Workflow> WorkflowAdded;

        /// <summary>
        /// An event that fires when an workflow is udpated.
        /// </summary>
        event EventHandler<Workflow> WorkflowUpdated;

        /// <summary>
        /// An event that fires when an workflow is removed.
        /// </summary>
        event EventHandler<Workflow> WorkflowRemoved;

        /// <summary>
        /// An event that fires after a state change has been applied to an object, so auditing,
        /// notifications and integrations can react to it without being coupled to the manager.
        /// </summary>
        event EventHandler<WorkflowTransitionResult> TransitionExecuted;

        /// <summary>
        /// Returns a workflow based on its id.
        /// </summary>
        /// <param name="workflowId">The id of the workflow.</param>
        /// <returns>The workflow.</returns>
        Workflow GetWorkflow(Guid workflowId);

        /// <summary>
        /// Returns a workflow together with the structure its state machine is made of: its
        /// participating states (including their category) and its transitions with both
        /// endpoints resolved.
        /// </summary>
        /// <remarks>
        /// <see cref="GetWorkflow(Guid)"/> reads the header only, which is all the list and
        /// detail views need. Callers that have to reason about the state machine — which states
        /// exist, which transition leads where — have to use this overload instead.
        /// </remarks>
        /// <param name="workflowId">The id of the workflow.</param>
        /// <returns>The workflow including states and transitions, or <c>null</c>.</returns>
        Workflow GetWorkflowWithStructure(Guid workflowId);

        /// <summary>
        /// Resolves the persisted payload of a workflow-backed field to one of the workflow's
        /// states.
        /// </summary>
        /// <remarks>
        /// The match is attempted first by normalized name (case-, space- and
        /// punctuation-insensitive, so <c>in_progress</c> matches <c>In Progress</c>) and then by
        /// status id, because the payload is written as a plain string by the generic value
        /// binder and by the seeder alike.
        /// </remarks>
        /// <param name="workflow">
        /// The workflow whose states are searched. Must have been loaded through
        /// <see cref="GetWorkflowWithStructure(Guid)"/>.
        /// </param>
        /// <param name="data">The persisted payload of the workflow field.</param>
        /// <returns>The matching state, or <c>null</c> when the payload matches none.</returns>
        Status ResolveStatus(Workflow workflow, string data);

        /// <summary>
        /// Returns the states an object in the supplied state may be moved to.
        /// </summary>
        /// <remarks>
        /// These are the targets of the workflow's active transitions that leave
        /// <paramref name="currentStatus"/>. An object that carries no resolvable state has not
        /// entered the state machine yet, so passing <c>null</c> returns the workflow's entry
        /// states instead (all participating states when the workflow declares no entry).
        /// </remarks>
        /// <param name="workflow">
        /// The workflow to walk. Must have been loaded through
        /// <see cref="GetWorkflowWithStructure(Guid)"/>.
        /// </param>
        /// <param name="currentStatus">The state to leave, or <c>null</c>.</param>
        /// <returns>The reachable states, in workflow order and without duplicates.</returns>
        IEnumerable<Status> GetTargetStatuses(Workflow workflow, Status currentStatus);

        /// <summary>
        /// Returns the states the supplied object may actually be moved to by the supplied
        /// identity: the reachable ones, narrowed to those whose transition the guards let
        /// through.
        /// </summary>
        /// <remarks>
        /// What a dropdown offers, as opposed to what the state machine allows: a move a guard
        /// refuses must not be shown, or the refusal arrives after the click and reads as a
        /// fault. An entry move carries no transition and therefore no guards.
        /// </remarks>
        /// <param name="workflow">The workflow to walk.</param>
        /// <param name="currentStatus">The state to leave, or <c>null</c>.</param>
        /// <param name="objectEntity">The object being moved.</param>
        /// <param name="field">The workflow-backed field carrying the state.</param>
        /// <param name="identityId">The identity that would make the move.</param>
        /// <returns>The states to offer.</returns>
        IEnumerable<Status> GetOfferedStatuses(Workflow workflow, Status currentStatus, Model.Entities.Object objectEntity, Field field, Guid identityId);

        /// <summary>
        /// Moves a workflow-backed field of an object to the requested state, enforcing the
        /// workflow server-side.
        /// </summary>
        /// <remarks>
        /// The call runs the stages the workflow concept prescribes, in this order:
        /// <list type="number">
        /// <item><description><b>reachability</b> - an active transition has to connect the
        /// current state to the requested one, or, for an object that has not entered the state
        /// machine yet, the requested state has to be an entry state;</description></item>
        /// <item><description><b>guards</b> - the condition the transition carries
        /// (<c>Transition.GuardExpression</c>, a disjunction of conjunctions over the keys of the
        /// registered guards) has to hold, and so does the relation guard that refuses a move
        /// into a closing state while something open blocks the object;</description></item>
        /// <item><description><b>validators</b> - the condition
        /// (<c>Transition.ValidatorExpression</c>) is evaluated against the object <em>and</em>
        /// what the screen of the transition carries, so a move that demands a note is refused
        /// before anything is written, naming what is missing;</description></item>
        /// <item><description><b>apply</b> - the state is written;</description></item>
        /// <item><description><b>post functions</b> - the built-in ones that keep the audit trail
        /// honest and close what follows, then the ones the transition names, in their
        /// order.</description></item>
        /// </list>
        /// All three configurable stages run inside the commit of the transition, so what a post
        /// function changes belongs to the same revision as the state change.
        /// </remarks>
        /// <param name="objectId">The id of the object whose state changes.</param>
        /// <param name="fieldId">The id of the workflow-backed field carrying the state.</param>
        /// <param name="targetStatusId">The id of the requested state.</param>
        /// <param name="identityId">
        /// The identity performing the change, stamped on the object. Pass
        /// <see cref="Guid.Empty"/> to leave the previous updater in place.
        /// </param>
        /// <param name="screenValues">
        /// What was filled in on the screen of the transition, keyed by field name, or
        /// <see langword="null"/> when the transition shows none. The values reach the validators
        /// before anything is written and the post functions afterwards.
        /// </param>
        /// <returns>The outcome of the state change.</returns>
        WorkflowTransitionResult ExecuteTransition(Guid objectId, Guid fieldId, Guid targetStatusId, Guid identityId, IReadOnlyDictionary<string, string> screenValues = null);

        /// <summary>
        /// Returns a workflow based on its id.
        /// </summary>
        /// <param name="workflowId">The id of the workflow.</param>
        /// <returns>The workflow.</returns>
        Workflow GetWorkflow(WorkflowIdParameter workflowId);

        /// <summary>
        /// Retrieves a collection of workflows that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// An enumerable collection of workflows that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Workflow> GetWorkflows(ClassIdParameter classId);

        /// <summary>
        /// Retrieves a collection of workflows that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workflows. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workflows that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Workflow> GetWorkflows(IQuery<Workflow> query);

        /// <summary>
        /// Retrieves a collection of workflows that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workflows. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workflows that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Workflow> GetWorkflows(IQuery<Workflow> query, IQueryContext context);

        /// <summary>
        /// Adds a workflow to the manager.
        /// </summary>
        /// <param name="workflowEntity">The workflow to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkflowManager Add(Workflow workflowEntity);

        /// <summary>
        /// Update a workflow to the manager.
        /// </summary>
        /// <param name="workflowEntity">The workflow to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkflowManager Update(Workflow workflowEntity);

        /// <summary>
        /// Removes the specified workflow from the manager.
        /// </summary>
        /// <remarks>This method removes the specified workflow from the manager. If the workflow does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="workflowId">The workflow id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IWorkflowManager Remove(Guid workflowId);
    }
}
