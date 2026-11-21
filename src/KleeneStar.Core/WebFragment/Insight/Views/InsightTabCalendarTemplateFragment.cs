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
    /// Tab template of an insight's calendar tab: its objects on a month, week or agenda grid,
    /// with search and quickfilter above it.
    /// </summary>
    [Section<WebExpress.WebUI.WebSection.SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(5)]
    [Cache]
    public sealed class InsightTabCalendarTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabCalendarTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Calendar)
        {
        }
    }

    /// <summary>
    /// The search of the calendar tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabCalendarTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabCalendarSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabCalendarSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildSearch<InsightCalendarResource>("id_insight_calendar_search"));
        }
    }

    /// <summary>
    /// The quickfilter bar of the calendar tab.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabCalendarTemplateFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabCalendarQuickfilterFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabCalendarQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildBoardQuickfilter<InsightCalendarResource>("id_insight_calendar_quickfilter"));
        }
    }

    /// <summary>
    /// The calendar of the calendar tab. Moving an entry writes the dates back; creating and
    /// deleting entries stays off, because objects are raised and retired through the object flow.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<InsightTabCalendarTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class InsightTabCalendarFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabCalendarFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Calendar>(service => service
                    .Method(HttpMethod.Get)
                    .Error(409, "kleenestar.core:object.view.plan.conflict"))
                .Resource<InsightCalendarResource>(resource => resource
                    .Service<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Calendar>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            var calendar = new ControlDataSchedule()
            {
                View = _ => TypeViewSchedule.Month,
                ShowWeekNumbers = _ => true,
                MiniCalendar = _ => true,
                Editable = _ => true,
                Creatable = _ => false,
                Deletable = _ => false
            };

            calendar.Resource<InsightCalendarResource>();

            Add(viewState, calendar);
        }
    }
}
