using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the Scrum sprint overview content of the objects index — the active
    /// iteration with progress and burn-down — rendered inside the
    /// <see cref="IssueTabScrumTemplateFragment"/> tab template below the scrum team
    /// workload (<see cref="IssueTabScrumSprintTeamFragment"/>, order 0) and above the
    /// board/backlog view control (<see cref="IssueTabScrumViewFragment"/>, order 2), so
    /// both views work under the same sprint header. The fragment IS the sprint
    /// control — it derives from the fragment-aware
    /// <see cref="FragmentControlDataScrumSprint"/> base and registers in
    /// <see cref="SectionTabTemplatePrimary"/>, the section the tab template collects
    /// its content from.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabScrumTemplateFragment>]
    [Order(1)]
    [Cache]
    public sealed class IssueTabScrumSprintFragment : FragmentControlDataScrumSprint
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumSprintFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<WWW.Api._1_.Objects._workspacekey_.ScrumSprint>().ToString());
        }
    }
}
