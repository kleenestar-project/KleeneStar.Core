using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the scrum team workload of the active sprint — the people working in the
    /// sprint with the story points committed to each and the share already completed —
    /// rendered inside the <see cref="IssueTabScrumTemplateFragment"/> tab template above
    /// the sprint burn-down (<see cref="IssueTabScrumSprintFragment"/>, order 1) and the
    /// board/backlog view control (<see cref="IssueTabScrumViewFragment"/>, order 2). The
    /// fragment IS the scrum team control — it derives from
    /// <see cref="FragmentControlDataScrumTeam"/> and registers in
    /// <see cref="SectionTabTemplatePrimary"/>, the section the tab template collects its
    /// content from. Its data comes from the sprint team endpoint. It leads the tab
    /// (order 0) and is shared by both panes of the switch.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabScrumTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabScrumSprintTeamFragment : FragmentControlDataScrumTeam
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumSprintTeamFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            ServiceFactory = _ => DataServiceDescriptor.Data(CoreHub.GetUri<WWW.Api._1_.Objects._workspacekey_.ScrumSprintTeam>().ToString());
        }
    }
}
