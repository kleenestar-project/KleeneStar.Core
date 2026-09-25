using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.SavedSearch
{
    /// <summary>
    /// The permission dialog of a saved search, opened from the search page while the saved
    /// search runs and from the row menu of the saved-search table. A group granted a
    /// <c>savedsearch_…</c> policy here sees the saved search in its sidebar and may do what
    /// the policy carries; without a grant the saved search is its owner's alone.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Permission>]
    [Cache]
    public sealed class SavedSearchPermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SavedSearchPermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.PermissionPolicies>();
        }
    }
}
