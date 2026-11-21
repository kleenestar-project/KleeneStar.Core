using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Provides the Kanban board content of the asset overview, rendered inside the
    /// <see cref="AssetTabKanbanTemplateFragment"/> tab template. Its data comes from the
    /// asset Kanban endpoint.
    /// </summary>
    /// <remarks>
    /// The board is the master side of a <see cref="ControlMasterDetail"/> whose frame shows
    /// the reduced reading view of the selected object, mirroring the issue board. The bridge
    /// endpoint resolves the object by id and forwards to the route of its kind, so the asset
    /// board needs no route knowledge of its own.
    ///
    /// The fragment also owns the <see cref="ControlViewState"/> carrying the tab's query
    /// surface, which the search (<see cref="AssetTabKanbanSearchFragment"/>) and the
    /// quickfilter (<see cref="AssetTabKanbanQuickfilterFragment"/>) write into.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<AssetTabKanbanTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class AssetTabKanbanFragment : FragmentControlPanel
    {
        /// <summary>
        /// Gets the board control forming the master side of the view.
        /// </summary>
        public ControlDataKanban Board { get; } = new ControlDataKanban()
        {
            // enable the full board editing surface; the endpoint persists every column and
            // swimlane change to the workspace's asset Kanban board configuration
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
        public AssetTabKanbanFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            // the pane shows the reduced reading view, the same one the list tab shows; the
            // bridge page resolves the object id the board reports in its selection event
            // onto the object key the per-kind route is addressed by, and "{id}" is
            // substituted by the master-detail controller
            // the ViewState declares the board resource and maps the shared state onto the
            // query parameters the endpoint reads: "q" for the search, "f" for the chips
            var viewState = new global::KleeneStar.Core.WebControl.TabBoardViewState(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Kanban>(service => service.Method(HttpMethod.Get))
                .Resource<AssetKanbanResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Kanban>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            Board.Resource<AssetKanbanResource>();

            var detailRoute = CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Preview>();
            var detailUri = $"{detailRoute}?id={{id}}";

            var masterDetail = new ControlMasterDetail(id + "-masterdetail", Board)
            {
                DetailUriTemplate = _ => detailUri,

                // as on the issue board: the columns have a 280px minimum, so the view
                // starts full width and a double click brings the detail in
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
