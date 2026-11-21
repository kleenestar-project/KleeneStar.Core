using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the backlog view of the Scrum tab — the sprint sections and the product
    /// backlog beside a detail pane for the selected item — rendered as a view item of the
    /// <see cref="IssueTabScrumViewFragment"/> view control, behind the board
    /// (<see cref="IssueTabScrumViewBoardFragment"/>, order 0).
    /// </summary>
    /// <remarks>
    /// The backlog control is the master side of a <see cref="ControlMasterDetail"/> whose
    /// frame loads the reduced reading view of the selected object on demand, so a row can be
    /// inspected without leaving the planning view.
    ///
    /// Like the board it carries no query surface of its own but renders the
    /// <see cref="IssueScrumBacklogResource"/> of
    /// <see cref="IssueTabScrumViewStateFragment"/>, so the search and the quickfilter in
    /// the view header describe one filter for both views.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<IssueTabScrumViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class IssueTabScrumViewBacklogFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the backlog control forming the master side of the view.
        /// </summary>
        public ControlDataScrumBacklog Backlog { get; } = new ControlDataScrumBacklog();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumViewBacklogFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            Icon = _ => new IconListCheck();
            Title = _ => "kleenestar.core:view.backlog.title";

            // the backlog data is a central resource the view's ViewState owns and loads, so
            // the control renders that slice instead of loading itself; selection and drag
            // state stay local to it
            Backlog.Resource<IssueScrumBacklogResource>();

            // the pane shows the reduced reading view, the same one the list view shows; the
            // bridge page resolves the object id the backlog reports in its selection event
            // onto the object key the per-kind route is addressed by, and "{id}" is
            // substituted by the master-detail controller
            var detailUri = $"{CoreHub.GetUri<WWW.Objects.Preview>()}?id={{id}}";

            var masterDetail = new ControlMasterDetail(id + "-masterdetail", Backlog)
            {
                DetailUriTemplate = _ => detailUri,
                MasterInitialSize = _ => 62,

                // the view fills the pane the shell gives it instead of bringing a height of
                // its own; the content panel opens the chain of panels down to the region as
                // soon as a filling one is on the page
                Fill = _ => true
            };

            masterDetail.Detail = new ControlFrame(id + "-frame")
            {
                Selector = _ => "#wx-content-main"
            };

            Add(masterDetail);
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
