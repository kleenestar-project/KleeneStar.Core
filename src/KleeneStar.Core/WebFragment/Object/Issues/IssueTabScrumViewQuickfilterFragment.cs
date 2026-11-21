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
    /// Quickfilter header of the scrum view: the personal scopes (assigned to me, starred)
    /// as toggleable chips, writing the active selection into the shared query state of
    /// <see cref="IssueTabScrumViewStateFragment"/>.
    /// </summary>
    /// <remarks>
    /// Registered as a view header rather than inside the board view item, so the chips are
    /// shown for the board and the backlog alike — the same placement
    /// <see cref="Class.ClassViewQuickfilterFragment"/> uses for the class views.
    /// </remarks>
    [Section<SectionViewHeaderSecondary>]
    [Scope<IssueTabScrumViewFragment>]
    [Cache]
    public sealed class IssueTabScrumViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_3E8B5D2C9A1F42B7C4D6E0F8A9B3C1D5";

        /// <summary>
        /// Gets the quickfilter control offering the personal sprint scopes.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new ControlDataQuickfilter(ContentId)
            .DataService<WWW.Api._1_.Objects._workspacekey_.ScrumSprintQuickfilter>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // writing the chip selection into the shared state re-queries the board
            // resource; the backlog reads the same state on its next load. the cast picks
            // the writing-surface overload of Resource<T> — without it the compiler settles
            // on the ControlDataList one and fails on the receiver type.
            ((IViewStateModelBound)Quickfilter).Resource<IssueSprintBoardResource>().Model("filter");

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
