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

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The impact analysis of an object, drawn as a graph: the object in the accent colour, what
    /// a change to it reaches around it, and the consequence each step carries as the label of
    /// its edge.
    /// </summary>
    /// <remarks>
    /// The picture is laid out by the viewer's own simulation rather than from stored positions:
    /// unlike a workflow, an impact graph is computed per request and per depth, so there is no
    /// canvas anybody authored and nothing to restore. What the reader arranges by hand stays
    /// for as long as they look at it, which is what the reading is for.
    /// <para>
    /// The analysis behind it walks the relations transitively; how far is the <c>depth</c> the
    /// endpoint is asked for, and the depth chooser above the canvas
    /// (<see cref="ObjectImpactDepthFragment"/>) is what sets it.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Impact>]
    [Cache]
    public sealed class ObjectImpactGraphFragment : FragmentControlDataGraphViewer
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectImpactGraphFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the viewer loads the graph with GET; the object of the page rides along in the
            // route and the requested depth as a query parameter, because a reading never writes
            // anything back
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(GetUri(renderContext)?.ToString());

            // nothing here was laid out by anybody, so the nodes are placed by the simulation
            // and a grid would only pretend that the positions mean something
            Physics = _ => true;

            // a definite height, because a percentage resolves against nothing in the content
            // region and the canvas would collapse to the viewer's own default strip - which is
            // half a screen too short to read a chain of records in
            // ...and each declaration ends in its own semicolon, because the control joins the
            // list with a space: two declarations without one become a single invalid rule that
            // the browser drops whole
            Styles = ["height: 65vh;", "min-height: 24rem;"];

            // the canvas is a single tab stop whose content is pure geometry, so without a name a
            // screen reader has nothing to announce it by
            Label = renderContext => I18N.Translate(renderContext, "kleenestar.core:object.impact.graph.label");
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c> when the
        /// request addresses no object.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return GetUri(renderContext) is null
                ? null
                : base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the address of the impact endpoint for the object the page addresses, carrying
        /// the depth the reader chose.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The bound route, or <see langword="null"/> when the request addresses no
        /// object.</returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            if (string.IsNullOrWhiteSpace(objectKey?.Value))
            {
                return null;
            }

            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Impact._objectkey_.Index>()?
                .Add(new UriQuery("depth", ObjectImpactDepth.Resolve(renderContext).ToString()))
                .BindParameters(renderContext.Request);
        }
    }
}
