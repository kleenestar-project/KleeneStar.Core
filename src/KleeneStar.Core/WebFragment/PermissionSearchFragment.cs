using KleeneStar.Core.WebFragment.Calendar;
using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Core.WebFragment.Dashboard;
using KleeneStar.Core.WebFragment.Workspace;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// The search box of the permission dialogs, contributed to the toolbar the permission surface
    /// draws above its assignment table.
    /// </summary>
    /// <remarks>
    /// The box used to be rendered by the dialog itself, above the surface, with a bind authored
    /// by hand. It now stands where the framework puts the tools of the surface: the surface
    /// collects the fragments of <see cref="SectionPermissionToolbarPrimary"/>, lifts them into
    /// its toolbar and binds a search box among them to itself, so no bind is declared here or on
    /// the dialogs - typing narrows the table to the groups whose name matches.
    /// <para>
    /// The toolbar sections resolve against the <b>runtime type</b> of the surface, and the scope
    /// match is exact: a scope naming the framework's base control would reach a surface authored
    /// as that control, not one derived from it. Every permission dialog of the core is a sealed
    /// fragment of its own, so each is named here - a fifth dialog gets the box by being added to
    /// the list, not by inheriting anything.
    /// </para>
    /// </remarks>
    [Section<SectionPermissionToolbarPrimary>]
    [Scope<ClassPermissionFragment>]
    [Scope<WorkspacePermissionFragment>]
    [Scope<DashboardPermissionFragment>]
    [Scope<CalendarPermissionFragment>]
    [Cache]
    public sealed class PermissionSearchFragment : FragmentControlSearch
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PermissionSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Placeholder = _ => "kleenestar.core:permission.search.placeholder";
            Icon = _ => new IconMagnifyingGlass();
        }
    }
}
