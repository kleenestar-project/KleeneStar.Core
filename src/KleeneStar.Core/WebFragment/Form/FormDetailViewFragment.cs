using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Represents a fragment control that displays the three fixed forms (create, edit, view) per
    /// class within a ControlView. This fragment is only rendered for standard forms. Additional
    /// forms do not have these predefined views and will display an empty or custom layout instead.
    /// </summary>
    /// <remarks>
    /// The editor is offered only where the form's class reads its forms - a class rendered as
    /// prose asks for no fields and shows none, so <see cref="FormProseNoticeFragment"/> takes
    /// this fragment's place there. See <see cref="FormRendererCondition"/>.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Condition<FormFieldRendererCondition>]
    [Cache]
    public sealed class FormDetailViewFragment : FragmentControlDataFormEditor
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FormDetailViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(GetUri(renderContext).ToString());

            // the editor is the whole page rather than a block among others, so it takes the
            // height the content region offers instead of growing with the form it edits:
            // otherwise the page scrolls around it and carries the form name above and the
            // save state below off the screen while the user is still working in the middle
            Fill = _ => true;
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
        /// Generates a URI for the form editor resource based on the specified render context.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context containing the request and parameters used to construct the URI.
        /// </param>
        /// <returns>
        /// An <see cref="IUri"/> representing the form editor resource with query parameters bound 
        /// from the render context. Returns null if the base URI cannot be resolved.
        /// </returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var formIdParam = renderContext.Request.GetParameter<FormIdParameter>();
            var restUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Forms.FormEditor>()?
                .Add(new UriQuery("id", formIdParam?.Value.ToString()))
                .BindParameters(formIdParam)
                .BindParameters(renderContext.Request);

            return restUri;
        }
    }
}
