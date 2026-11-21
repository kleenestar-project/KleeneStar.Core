using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Search header of the issue Gantt tab: an advanced-search control whose prompt
    /// history and lookahead come from the issue Wql endpoint, while the entered query is
    /// written into the shared query state of <see cref="IssueTabGanttFragment"/>.
    /// </summary>
    /// <remarks>
    /// It leads the tab (order 0), above the quickfilter and the plan, and drives the plan
    /// through the <see cref="IssueGanttResource"/> binding rather than through a bind wire
    /// — the Gantt control has none.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabGanttTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabGanttSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_2D8F5B04D7E3592AB6F1C4D9E8A03B27";

        /// <summary>
        /// Gets the search control used to query the plan.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new ControlAdvancedSearch(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Wql>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabGanttSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the cast picks the writing-surface overload of Resource<T> — without it the
            // compiler settles on the ControlDataList one and fails on the receiver type
            ((IViewStateModelBound)Search).Resource<IssueGanttResource>().Model("search");

            Add(Search);
        }
    }
}
