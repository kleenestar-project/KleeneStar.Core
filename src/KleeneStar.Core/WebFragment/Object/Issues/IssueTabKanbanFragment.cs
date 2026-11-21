using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the Kanban board content of the objects index, rendered inside the
    /// <see cref="IssueTabKanbanTemplateFragment"/> tab template.
    /// </summary>
    /// <remarks>
    /// The board is the master side of a <see cref="ControlMasterDetail"/> whose frame shows
    /// the reduced reading view of the selected object, so a card can be inspected without
    /// leaving the board — the same pane the list view and the scrum views show. Selecting a
    /// card is the board's own behaviour; the composite only owns the detail side.
    ///
    /// The fragment also owns the <see cref="ControlViewState"/> that carries the tab's
    /// query surface. The search (<see cref="IssueTabKanbanSearchFragment"/>) and the
    /// quickfilter (<see cref="IssueTabKanbanQuickfilterFragment"/>) write into it and the
    /// board renders the <see cref="IssueKanbanResource"/> it feeds, so a chip or a search
    /// term re-queries the board — the Kanban control has no <c>BindFilter</c> of its own.
    /// All three resolve the ViewState by resource type, so they may sit in sibling
    /// fragments.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabKanbanTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class IssueTabKanbanFragment : FragmentControlPanel
    {
        /// <summary>
        /// Gets the board control forming the master side of the view.
        /// </summary>
        public ControlDataKanban Board { get; } = new ControlDataKanban()
        {
            // enable the full board editing surface; the endpoint persists every column and
            // swimlane change to the workspace's issue Kanban board configuration
            EditableColumn = _ => true,
            MovableColumn = _ => true,
            DeletableColumn = _ => true,
            AddableColumn = _ => true,
            AddableSwimlane = _ => true,
            EditableSwimlane = _ => true,
            DeletableSwimlane = _ => true,
            MovableSwimlane = _ => true,
            ConfigurableBoard = _ => true,
            ConfigurableSwimlane = _ => true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabKanbanFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            // the ViewState declares the board resource and maps the shared state onto the
            // query parameters the endpoint reads: "q" for the search, "f" for the chips
            var viewState = new global::KleeneStar.Core.WebControl.TabBoardViewState(id + "-viewstate")
                .State(_ => { })
                .Service<WWW.Api._1_.Objects._workspacekey_.Kanban>(service => service.Method(HttpMethod.Get))
                .Resource<IssueKanbanResource>(resource => resource
                    .Service<WWW.Api._1_.Objects._workspacekey_.Kanban>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            Board.Resource<IssueKanbanResource>();

            // the pane shows the reduced reading view, the same one the list tab shows: a
            // detail frame embeds a page's main content region, and that region of the full
            // reading view is written for a full-width column. The bridge page resolves the
            // object id the board reports in its selection event onto the object key the
            // per-kind route is addressed by; "{id}" is substituted by the controller
            var detailUri = $"{CoreHub.GetUri<WWW.Objects.Preview>()}?id={{id}}";

            var masterDetail = new ControlMasterDetail(id + "-masterdetail", Board)
            {
                DetailUriTemplate = _ => detailUri,

                // a board needs its width: the columns have a 280px minimum, so a
                // permanently open detail would push them into a horizontal scroll. The
                // view therefore starts as the full-width board, a double click brings the
                // detail in, and closing it gives the width back.
                DetailVisible = _ => false,
                Reveal = _ => TypeMasterDetailReveal.DoubleClick,
                MasterInitialSize = _ => 62,

                // the board is the view of the tab rather than one block on a page of them,
                // so it takes the height the content panel offers instead of bringing one of
                // its own — the panel opens the chain of panels down to it as soon as a
                // filling region is on the page, so this is all the view has to declare
                Fill = _ => true,
                Detail = new ControlFrame(id + "-frame")
                {
                    Selector = _ => "#wx-content-main"
                }
            };

            Add(viewState, masterDetail);
        }
    }
}
