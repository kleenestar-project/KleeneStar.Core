using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workflow
{
    /// <summary>
    /// Represents a read-only graph view of a workflow: its states as the nodes and its transitions
    /// as the edges, laid out as the designer stored them.
    /// </summary>
    /// <remarks>
    /// The graph the workflow is authored in belongs to the designer, which owns the editing
    /// surface and the write path; this fragment only shows the workflow the page addresses.
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workflow._workflowid_.Flow>]
    [Cache]
    public sealed class WorkflowFlowFragment : FragmentControlDataGraphViewer
    {

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkflowFlowFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the viewer loads the graph with GET; the workflow of the page rides along as the id
            // query parameter, because a read-only viewer never writes anything back
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(GetUri(renderContext)?.ToString());

            // the designer stores the canvas positions per workflow, so the same grid makes the
            // viewer read as the surface the layout was authored on
            Grid = _ => 20;

            // the canvas is a single tab stop whose content is pure geometry, so without a name a
            // screen reader has nothing to announce it by
            Label = renderContext => I18N.Translate(renderContext, "kleenestar.core:workflow.flow.label");
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
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Generates a URI for the workflow graph resource based on the specified render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context containing the request and parameters used to construct the URI.
        /// </param>
        /// <returns>
        /// An <see cref="IUri"/> addressing the graph of the workflow the page shows. Returns null
        /// if the base URI cannot be resolved.
        /// </returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var workflowIdParameter = renderContext?.Request?.GetParameter<WorkflowIdParameter>();

            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workflows.Graph>()?
                .Add(new UriQuery("id", workflowIdParameter?.Value))
                .BindParameters(renderContext?.Request);
        }
    }
}
