using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Calendar
{
    /// <summary>
    /// The permission dialog of a calendar, opened from the row menu of the calendar overview.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Calendar._calendarid_.Permission>]
    [Cache]
    public sealed class CalendarPermissionFragment : FragmentControlDataPermission
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarPermissionFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Calendar._calendarid_.Permission>();
            this.GroupsService<global::KleeneStar.Core.WWW.Api._1_.Calendar._calendarid_.PermissionGroups>();
            this.PoliciesService<global::KleeneStar.Core.WWW.Api._1_.Calendar._calendarid_.PermissionPolicies>();
        }
    }
}
