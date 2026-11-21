using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workflow
{
    /// <summary>
    /// Represents a fragment control that displays the description of a workflow in Markdown format 
    /// within a workflow context.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workflow._workflowid_.Index>]
    [Cache]
    public sealed class WorkflowDescriptionFragment : FragmentControlText
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public WorkflowDescriptionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Format = _ => TypeFormatText.Markdown;
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var workflowIdParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();
            var workflowId = Guid.TryParse(workflowIdParameter?.Value, out var result) ? result : Guid.Empty;
            var workflow = CoreHub.WorkflowManager.GetWorkflow(workflowId);

            var text = workflow?.Description;

            return base.Render(renderContext, visualTree);
        }
    }
}
