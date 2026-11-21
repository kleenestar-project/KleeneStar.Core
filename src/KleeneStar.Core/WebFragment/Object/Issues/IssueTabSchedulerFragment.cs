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
    /// Provides the calendar content of the objects index, rendered inside the
    /// <see cref="IssueTabSchedulerTemplateFragment"/> tab template.
    /// </summary>
    /// <remarks>
    /// The fragment owns the <see cref="ControlViewState"/> that carries the tab's query
    /// surface. The search (<see cref="IssueTabSchedulerSearchFragment"/>) and the quickfilter
    /// (<see cref="IssueTabSchedulerQuickfilterFragment"/>) write into it and the calendar
    /// renders the <see cref="IssueSchedulerResource"/> it feeds, so a chip or a search term
    /// re-queries the calendar. All three resolve the ViewState by resource type, so they may
    /// sit in sibling fragments.
    /// <para>
    /// Entries are movable — the drop persists the new span into the date fields of the
    /// object's class — but not creatable or deletable: an object is raised and retired
    /// through the object flow, so offering the gestures would promise what the endpoint
    /// refuses.
    /// </para>
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabSchedulerTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class IssueTabSchedulerFragment : FragmentControlPanel
    {
        /// <summary>
        /// Resource key of the message shown when the endpoint refuses a move because the
        /// object's class models no date field for the edge that was dragged. The calendar
        /// reloads the shown period on a refusal, so the entry snaps back and this explains why.
        /// </summary>
        public const string ConflictResource = "kleenestar.core:object.view.plan.conflict";

        /// <summary>
        /// Gets the calendar control of the view.
        /// </summary>
        public ControlDataSchedule Calendar { get; } = new ControlDataSchedule()
        {
            View = _ => TypeViewSchedule.Month,
            ShowWeekNumbers = _ => true,
            MiniCalendar = _ => true,
            Editable = _ => true,

            // the endpoint refuses both, so the gestures stay off rather than failing on drop
            Creatable = _ => false,
            Deletable = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabSchedulerFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            // the ViewState declares the calendar resource and maps the shared state onto the
            // query parameters the endpoint reads: "q" for the search, "f" for the chips. The
            // period travels separately, as the from/to parameters the control appends.
            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<WWW.Api._1_.Objects._workspacekey_.Scheduler>(service => service
                    .Method(HttpMethod.Get)
                    .Error(409, ConflictResource))
                .Resource<IssueSchedulerResource>(resource => resource
                    .Service<WWW.Api._1_.Objects._workspacekey_.Scheduler>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            Calendar.Resource<IssueSchedulerResource>();

            Add(viewState, Calendar);
        }
    }
}
