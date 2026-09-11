using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Dashboard
{
    /// <summary>
    /// The permission dialog of a dashboard, opened from the row menu of the dashboard overview.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Dashboard._dashboardid_.Permission>]
    [Cache]
    public sealed class DashboardPermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public DashboardPermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Dashboard._dashboardid_.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.Dashboard._dashboardid_.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.Dashboard._dashboardid_.PermissionPolicies>();
        }
    }
}
