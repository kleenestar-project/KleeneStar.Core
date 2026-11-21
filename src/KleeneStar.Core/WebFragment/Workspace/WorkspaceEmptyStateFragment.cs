using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Empty-state placeholder rendered on the workspace overview when no workspace exists yet.
    /// Uses the standard <see cref="ControlEmptyState"/> pattern from the WebExpress.WebUI
    /// framework so the page still communicates the available next step.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkspaceEmptyStateCondition"/> gates this fragment and its complement gates
    /// <see cref="WorkspaceViewFragment"/>, so the page shows exactly one of the two.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces.Index>]
    [Cache]
    [Condition<WorkspaceEmptyStateCondition>]
    public sealed class WorkspaceEmptyStateFragment : FragmentControlEmptyState
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment. Cannot be null.
        /// </param>
        public WorkspaceEmptyStateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconInbox();
            Title = _ => "kleenestar.core:workspace.empty.title";
            Message = _ => "kleenestar.core:workspace.empty.message";
        }
    }
}
