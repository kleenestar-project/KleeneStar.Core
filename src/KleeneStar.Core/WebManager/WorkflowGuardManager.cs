using KleeneStar.Core.WebWorkflow;
using KleeneStar.Core.WebWorkflow.Guards;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The registry of the conditions that decide whether a transition may be taken, seeded with
    /// the guards the core ships.
    /// </summary>
    /// <remarks>
    /// The standard guards are the ones every installation turns out to want and none of them
    /// knows anything about a particular workflow: they ask about the object, the person making
    /// the move and what the object is connected to. An installation that needs more contributes
    /// them from a plugin - <see cref="WorkflowRuleManagerBase{TRule}.Register"/> is the whole
    /// seam, and a workflow storing a key of an uninstalled plugin keeps working, with the term
    /// simply failing to hold.
    /// </remarks>
    public sealed class WorkflowGuardManager : WorkflowRuleManagerBase<IWorkflowGuard>, IWorkflowGuardManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WorkflowGuardManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            Register(new AssigneeSetGuard());
            Register(new ActorIsAssigneeGuard());
            Register(new ActorIsCreatorGuard());
            Register(new NoOpenBlockersGuard());
            Register(new ChildrenCompletedGuard());
        }
    }
}
