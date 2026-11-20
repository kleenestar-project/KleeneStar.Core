using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The wizard that clones a workspace (<see cref="WorkspaceWizardFragmentBase"/>).
    /// </summary>
    /// <remarks>
    /// The steps start on the values <c>/api/1/workspaces</c> answers in clone mode: key and name
    /// of the original with a copy suffix, its categories, description, inheritance, access
    /// modifier, seal and tenants, and the state active.
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Clone>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class WorkspaceCloneFormFragment : WorkspaceWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Clone;
            FinishLabel = _ => "webexpress.webapp:clone.label";
            FinishIcon = _ => new IconClone();
        }
    }
}
