using KleeneStar.Model.Entities;
using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's Gantt tab: its objects on a timeline, with search and
    /// quickfilter above it.
    /// </summary>
    [Section<WebExpress.WebUI.WebSection.SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(4)]
    [Cache]
    public sealed class InsightTabGanttTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabGanttTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Gantt)
        {
        }
    }

    /// <summary>
    /// The search of the Gantt tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabGanttTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabGanttSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabGanttSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildSearch<InsightGanttResource>("id_insight_gantt_search"));
        }
    }

    /// <summary>
    /// The quickfilter bar of the Gantt tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabGanttTemplateFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabGanttQuickfilterFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabGanttQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildBoardQuickfilter<InsightGanttResource>("id_insight_gantt_quickfilter"));
        }
    }

    /// <summary>
    /// The plan of the Gantt tab. Moving a bar writes the dates back; a move onto an edge the
    /// class models no date field for answers 409 with a message of its own.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabGanttTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class InsightTabGanttFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabGanttFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Gantt>(service => service
                    .Method(HttpMethod.Get)
                    .Error(409, "kleenestar.core:object.view.plan.conflict"))
                .Resource<InsightGanttResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Gantt>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            var plan = new ControlDataGantt()
            {
                Scale = _ => "week",
                Columns = _ => "name,start,end,duration,progress,resources",
                Fill = _ => true
            };

            plan.Resource<InsightGanttResource>();

            Add(viewState, plan);
        }
    }
}
