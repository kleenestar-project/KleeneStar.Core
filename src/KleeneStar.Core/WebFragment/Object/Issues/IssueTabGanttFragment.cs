using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the timeline content of the objects index, rendered inside the
    /// <see cref="IssueTabGanttTemplateFragment"/> tab template.
    /// </summary>
    /// <remarks>
    /// The fragment owns the <see cref="ControlViewState"/> that carries the tab's query
    /// surface. The search (<see cref="IssueTabGanttSearchFragment"/>) and the quickfilter
    /// (<see cref="IssueTabGanttQuickfilterFragment"/>) write into it and the plan renders the
    /// <see cref="IssueGanttResource"/> it feeds, so a chip or a search term re-queries the
    /// plan — the Gantt control has no <c>BindFilter</c> of its own. All three resolve the
    /// ViewState by resource type, so they may sit in sibling fragments.
    /// <para>
    /// The plan is editable: dragging a bar persists the new span into the date fields of the
    /// object's class. Adding and removing bars is refused by the endpoint, because an object
    /// is raised and retired through the object flow.
    /// </para>
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabGanttTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class IssueTabGanttFragment : FragmentControlPanel
    {
        /// <summary>
        /// Resource key of the message shown when the endpoint refuses a move because the
        /// object's class models no date field for the edge that was dragged.
        /// </summary>
        public const string ConflictResource = "kleenestar.core:object.view.plan.conflict";

        /// <summary>
        /// Gets the timeline control of the view.
        /// </summary>
        public ControlDataGantt Plan { get; } = new ControlDataGantt()
        {
            // a plan of issues is read at the week scale far more often than at the day one,
            // and all three scales stay available from the toolbar
            Scale = _ => "week",
            Columns = _ => "name,start,end,duration,progress,resources",
            // the plan is the whole tab rather than a block among others, so it takes the
            // height the content region offers instead of bringing its own: a chart with a
            // height of its own either leaves dead space under it or reaches past the pane,
            // which then scrolls around a grid and a timeline that already scroll in step
            Fill = _ => true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabGanttFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            // the ViewState declares the plan resource and maps the shared state onto the
            // query parameters the endpoint reads: "q" for the search, "f" for the chips.
            // Error(409, …) gives the endpoint's 409 — a move onto an edge the class models no
            // date field for — a server-authored, localizable message instead of the client's
            // bare "request failed with status 409".
            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<WWW.Api._1_.Objects._workspacekey_.Gantt>(service => service
                    .Method(HttpMethod.Get)
                    .Error(409, ConflictResource))
                .Resource<IssueGanttResource>(resource => resource
                    .Service<WWW.Api._1_.Objects._workspacekey_.Gantt>()
                    .Param("f", "filter")
                    .Param("q", "search"));

            Plan.Resource<IssueGanttResource>();

            Add(viewState, Plan);
        }
    }
}
