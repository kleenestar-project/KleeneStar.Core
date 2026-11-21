using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// The permission dialog of an insight, opened from the row menu of the insight overview.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Permission>]
    [Cache]
    public sealed class InsightPermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightPermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Insight._insightid_.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.Insight._insightid_.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.Insight._insightid_.PermissionPolicies>();
        }
    }
}
