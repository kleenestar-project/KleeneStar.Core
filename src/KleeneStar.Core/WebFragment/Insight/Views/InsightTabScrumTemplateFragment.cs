using KleeneStar.Model.Entities;
using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's Scrum tab: the sprint board and the backlog in one view,
    /// over the sprints of the workspaces the insight's objects live in.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(3)]
    [Cache]
    public sealed class InsightTabScrumTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Scrum)
        {
        }
    }

    /// <summary>
    /// The view control of the Scrum tab, carrying the board and the backlog as two views.
    /// </summary>
    [Section<WebExpress.WebApp.WebSection.SectionTabTemplatePrimary>]
    [Scope<InsightTabScrumTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabScrumViewFragment : FragmentControlView, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Layout = _ => TypeLayoutView.ToggleGroup;
        }
    }

    /// <summary>
    /// The shared query state of the Scrum tab: the board and the backlog as two resources over
    /// one search term and one quickfilter selection.
    /// </summary>
    [Section<SectionViewHeaderPreferences>]
    [Scope<InsightTabScrumViewFragment>]
    [Cache]
    public sealed class InsightTabScrumStateFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumStateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            Add(new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.ScrumBoard>(service => service.Method(HttpMethod.Get))
                .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.ScrumBacklog>(service => service.Method(HttpMethod.Get))
                .Resource<InsightScrumBoardResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.ScrumBoard>()
                    .Param("f", "filter")
                    .Param("q", "search"))
                .Resource<InsightScrumBacklogResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.ScrumBacklog>()
                    .Param("f", "filter")
                    .Param("q", "search")));
        }
    }

    /// <summary>
    /// The search of the Scrum tab, shared by the board and the backlog.
    /// </summary>
    [Section<SectionViewHeaderPrimary>]
    [Scope<InsightTabScrumViewFragment>]
    [Cache]
    public sealed class InsightTabScrumSearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildSearch<InsightScrumBoardResource>("id_insight_scrum_search"));
        }
    }

    /// <summary>
    /// The quickfilter bar of the Scrum tab, shared by the board and the backlog.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<InsightTabScrumViewFragment>]
    [Cache]
    public sealed class InsightTabScrumQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildBoardQuickfilter<InsightScrumBoardResource>("id_insight_scrum_quickfilter"));
        }
    }

    /// <summary>
    /// The sprint board of the Scrum tab: the insight's objects committed to a running sprint.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<InsightTabScrumViewFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabScrumBoardFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumBoardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            Icon = _ => new IconTableColumns();
            Title = _ => "kleenestar.core:view.board.title";

            var board = InsightBoard.Build(id);
            board.Resource<InsightScrumBoardResource>();

            Add(InsightBoard.WithDetail(id, board));
        }
    }

    /// <summary>
    /// The backlog of the Scrum tab: the sprints and the objects planned into them or not yet.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<InsightTabScrumViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabScrumBacklogFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabScrumBacklogFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            Icon = _ => new IconListCheck();
            Title = _ => "kleenestar.core:view.backlog.title";

            var backlog = new ControlDataScrumBacklog();
            backlog.Resource<InsightScrumBacklogResource>();

            Add(InsightBoard.WithDetail(id, backlog, doubleClick: false));
        }
    }
}
