using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The permission dialog of a workspace: the group rows with the policies each group holds, and
    /// the row that adds a further group.
    /// </summary>
    /// <remarks>
    /// The surface is the framework's permission control rather than a pair of selects on a form.
    /// Granting is not editing a record: a workspace holds any number of group-to-policy grants,
    /// each added and withdrawn on its own, which a form bound to one record cannot express — the
    /// predecessor here could only ever have submitted a single pair, and posted it to the
    /// workspace's own crud endpoint at that.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Permissions>]
    [Policy<WorkspaceAdminPolicy>]
    [Cache]
    public sealed class WorkspacePermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspacePermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.PermissionPolicies>();
        }
    }
}
