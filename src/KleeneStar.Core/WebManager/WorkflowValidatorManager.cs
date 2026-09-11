using KleeneStar.Core.WebWorkflow;
using KleeneStar.Core.WebWorkflow.Validators;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The registry of the conditions a transition validates the object against, seeded with the
    /// validators the core ships.
    /// </summary>
    /// <remarks>
    /// A validator differs from a guard in what its failure means rather than in what it asks:
    /// it is evaluated when the move is actually made, with the screen filled in, and it reports
    /// a sentence saying what has to happen first. The standard set covers the fields every
    /// object carries and the two things a transition screen usually asks for - a note and an
    /// assignee. Anything class-specific is a plugin's business.
    /// </remarks>
    public sealed class WorkflowValidatorManager : WorkflowRuleManagerBase<IWorkflowValidator>, IWorkflowValidatorManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WorkflowValidatorManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            Register(new SummarySetValidator());
            Register(new DescriptionSetValidator());
            Register(new AssigneeSetValidator());
            Register(new ScreenNoteGivenValidator());
            Register(new AttachmentPresentValidator());
        }
    }
}
