using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Search header of the issue calendar tab: an advanced-search control whose prompt
    /// history and lookahead come from the issue Wql endpoint, while the entered query is
    /// written into the shared query state of <see cref="IssueTabSchedulerFragment"/>.
    /// </summary>
    /// <remarks>
    /// It leads the tab (order 0), above the quickfilter and the calendar, and drives the calendar
    /// through the <see cref="IssueSchedulerResource"/> binding rather than through a bind wire
    /// — the schedule control has none.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabSchedulerTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabSchedulerSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_3E9A6C15E8F46B08C7A2D5EAF9B14C38";

        /// <summary>
        /// Gets the search control used to query the calendar.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new ControlAdvancedSearch(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Wql>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabSchedulerSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the cast picks the writing-surface overload of Resource<T> — without it the
            // compiler settles on the ControlDataList one and fails on the receiver type
            ((IViewStateModelBound)Search).Resource<IssueSchedulerResource>().Model("search");

            Add(Search);
        }
    }
}
