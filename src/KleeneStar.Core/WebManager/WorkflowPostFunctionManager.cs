using KleeneStar.Core.WebWorkflow;
using KleeneStar.Core.WebWorkflow.PostFunctions;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The registry of the actions a transition performs once it has been applied, seeded with
    /// the post functions the core ships.
    /// </summary>
    /// <remarks>
    /// The standard set is what a state change usually has to do besides changing the state:
    /// take ownership, hand it back, write down what was decided, and tell the people watching.
    /// Each runs inside the commit of the transition, so the history reads as one action.
    /// </remarks>
    public sealed class WorkflowPostFunctionManager : WorkflowRuleManagerBase<IWorkflowPostFunction>, IWorkflowPostFunctionManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WorkflowPostFunctionManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            Register(new AssignToActorPostFunction());
            Register(new ClearAssigneePostFunction());
            Register(new ScreenNoteAsCommentPostFunction());
            Register(new WatchActorPostFunction());
        }
    }
}
