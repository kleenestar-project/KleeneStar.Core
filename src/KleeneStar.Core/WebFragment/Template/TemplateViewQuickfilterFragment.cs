using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents a fragment that provides a quick filter control for REST-based template queries in
    /// the template view.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<TemplateViewFragment>]
    [Cache]
    public sealed class TemplateViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content.
        /// </summary>
        public static readonly string ContentId = "id_8B2E1D4F7C5A3E9D6F0B1A8C2E5D9F4A";

        /// <summary>
        /// Represents the unique identifier of the class dropdown.
        /// </summary>
        private const string ClassFilterId = "id_5C9A4E7B2D6F1A8C3E0B5D9F7A2C4E61";

        /// <summary>
        /// Gets the quick filter control for REST-based template queries.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new ControlDataQuickfilter(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_.Quickfilter>().ToString())
        };

        /// <summary>
        /// Gets the dropdown that narrows the overview to the templates of a single class.
        /// </summary>
        /// <remarks>
        /// The classes are loaded from their own endpoint rather than authored here, so the menu
        /// lists exactly the classes the workspace's templates are bound to. It is a single-choice
        /// dropdown, which makes its options exclusive within the group — picking a class replaces
        /// the previous choice.
        /// </remarks>
        public ControlDataQuickfilterItemDropdown ClassFilter { get; } = new(ClassFilterId)
        {
            Text = _ => "kleenestar.core:template.class.label",
            Icon = _ => new IconClass(),
            Group = _ => global::KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_.Classes.FilterGroup,
            Multiple = _ => false,
            // the control writes the uri into its data attribute as it is, so the workspace of
            // the route has to be bound here — unlike a data service island, whose placeholders
            // the binding pass fills in later
            RestEndpoint = renderContext => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_.Classes>()?
                .BindParameters(renderContext.Request)
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Quickfilter.Add(ClassFilter);

            Add(Quickfilter);
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
    }
}
