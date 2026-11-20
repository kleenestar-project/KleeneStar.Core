using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The wizard that edits a workspace (<see cref="WorkspaceWizardFragmentBase"/>).
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Edit>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class WorkspaceEditFormFragment : WorkspaceWizardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Mode = _ => TypeRestFormMode.Edit;
            FinishLabel = _ => "kleenestar.core:workspace.edit.submit.label";
            FinishIcon = _ => new IconSave();
        }
    }
}
