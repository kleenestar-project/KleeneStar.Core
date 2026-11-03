using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// The permission dialog of the insight overview: which groups may create insights.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insights.Permission>]
    [Cache]
    public sealed class InsightCreationPermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightCreationPermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Insights.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.Insights.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.Insights.PermissionPolicies>();
        }
    }
}
