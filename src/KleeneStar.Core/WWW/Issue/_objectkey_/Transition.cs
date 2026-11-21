using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Represents the dialog a transition shows before it runs, when the workflow gives the
    /// transition a form.
    /// </summary>
    /// <remarks>
    /// The field and the target state arrive as the query parameters the transition endpoint
    /// takes (<see cref="WebParameter.FieldIdParameter"/>,
    /// <see cref="WebParameter.WorkflowStateIdParameter"/>), so the status picker addresses
    /// both the same way; the form itself is
    /// <see cref="WebFragment.Object.ObjectTransitionFormFragment"/>.
    /// </remarks>
    [WebIcon<IconWorkflow>]
    [Title("kleenestar.core:object.transition.screen.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Transition : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Transition()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
