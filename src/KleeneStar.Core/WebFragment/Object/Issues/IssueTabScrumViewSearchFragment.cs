using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Search header of the scrum view: an advanced-search control whose prompt history and
    /// lookahead come from the issue Wql endpoint, while the entered query is written into
    /// the shared query state of <see cref="IssueTabScrumViewStateFragment"/>.
    /// </summary>
    /// <remarks>
    /// Registered as a view header rather than inside a view item, so it is shown for the
    /// board and the backlog alike — the same placement
    /// <see cref="Class.ClassViewSearchFragment"/> uses for the class views.
    /// </remarks>
    [Section<SectionViewHeaderPrimary>]
    [Scope<IssueTabScrumViewFragment>]
    [Cache]
    public sealed class IssueTabScrumViewSearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_9C4F1A7E3B2D48F5A6E0C8B1D7F3A2E9";

        /// <summary>
        /// Gets the search control used to query the sprint board and the backlog.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new ControlAdvancedSearch(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Wql>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumViewSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // writing the search term into the shared state re-queries the board resource;
            // the backlog reads the same state on its next load. the cast picks the
            // writing-surface overload of Resource<T> — without it the compiler settles on
            // the ControlDataList one and fails on the receiver type.
            ((IViewStateModelBound)Search).Resource<IssueSprintBoardResource>().Model("search");

            Add(Search);
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
