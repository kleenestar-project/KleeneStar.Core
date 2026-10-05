using KleeneStar.Model.Entities;
using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's Kanban tab: the insight's board with its search and
    /// quickfilter above it.
    /// </summary>
    [Section<WebExpress.WebUI.WebSection.SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(2)]
    [Cache]
    public sealed class InsightTabKanbanTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabKanbanTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Kanban)
        {
        }
    }

    /// <summary>
    /// The search of the Kanban tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabKanbanTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabKanbanSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabKanbanSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildSearch<InsightKanbanResource>("id_insight_kanban_search"));
        }
    }

    /// <summary>
    /// The quickfilter bar of the Kanban tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabKanbanTemplateFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabKanbanQuickfilterFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabKanbanQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildBoardQuickfilter<InsightKanbanResource>("id_insight_kanban_quickfilter"));
        }
    }

    /// <summary>
    /// The board of the Kanban tab, the master side of a master-detail whose frame shows the
    /// reduced reading view of a card. A drop is a workflow transition.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabKanbanTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class InsightTabKanbanFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabKanbanFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Kanban>(service => service.Method(HttpMethod.Get))
                .Resource<InsightKanbanResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Kanban>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            var board = InsightBoard.Build(id);
            board.Resource<InsightKanbanResource>();

            Add(viewState, InsightBoard.WithDetail(id, board));
        }
    }

    /// <summary>
    /// Builds the boards of an insight - the Kanban tab's and the Scrum tab's sprint board.
    /// </summary>
    internal static class InsightBoard
    {
        /// <summary>
        /// Builds a board with the full editing surface; the endpoint refuses an arrangement by a
        /// caller who may not change the insight.
        /// </summary>
        /// <param name="id">The id prefix.</param>
        /// <returns>The board.</returns>
        public static ControlDataKanban Build(string id)
        {
            return new ControlDataKanban(id + "-board")
            {
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
        }

        /// <summary>
        /// Puts a master control beside the reduced reading view of the selected object.
        /// </summary>
        /// <param name="id">The id prefix.</param>
        /// <param name="master">The master control.</param>
        /// <param name="doubleClick">Whether the detail opens on a double click (a board needs its width).</param>
        /// <returns>The master-detail composite.</returns>
        public static ControlMasterDetail WithDetail(string id, IControl master, bool doubleClick = true)
        {
            // the bridge page resolves the object id the master reports onto the per-kind route
            var preview = CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Preview>();
            var detailUri = $"{preview}?id={{id}}";

            var masterDetail = new ControlMasterDetail(id + "-masterdetail", master)
            {
                DetailUriTemplate = _ => detailUri,
                MasterInitialSize = _ => 62,
                Fill = _ => true,
                Detail = new ControlFrame(id + "-frame")
                {
                    Selector = _ => "#wx-content-main"
                }
            };

            if (doubleClick)
            {
                masterDetail.DetailVisible = _ => false;
                masterDetail.Reveal = _ => TypeMasterDetailReveal.DoubleClick;
            }

            return masterDetail;
        }
    }
}
