using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Search header of the issue Kanban tab: an advanced-search control whose prompt
    /// history and lookahead come from the issue Wql endpoint, while the entered query is
    /// written into the shared query state of <see cref="IssueTabKanbanFragment"/>.
    /// </summary>
    /// <remarks>
    /// It leads the tab (order 0), above the quickfilter and the board, and drives the board
    /// through the <see cref="IssueKanbanResource"/> binding rather than through a bind wire
    /// — the Kanban control has none.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabKanbanTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabKanbanSearchFragment : FragmentControlPanel
    {
        /// <summary>
        /// Represents the unique identifier for the content used in the application.
        /// </summary>
        public static readonly string ContentId = "id_1B7E4A93C6D2481FA5E0B3C8D7F92A16";

        /// <summary>
        /// Gets the search control used to query the board.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new ControlAdvancedSearch(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Wql>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabKanbanSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the cast picks the writing-surface overload of Resource<T> — without it the
            // compiler settles on the ControlDataList one and fails on the receiver type
            ((IViewStateModelBound)Search).Resource<IssueKanbanResource>().Model("search");

            Add(Search);
        }
    }
}
