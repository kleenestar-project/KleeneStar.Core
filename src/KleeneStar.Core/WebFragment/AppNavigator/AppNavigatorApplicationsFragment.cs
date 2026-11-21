using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;

namespace KleeneStar.Core.WebFragment.AppNavigator
{
    /// <summary>
    /// Contributes the list of installed applications to the preferences area of the app navigator
    /// of the KleeneStar application.
    /// </summary>
    [Section<SectionAppPreferences>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class AppNavigatorApplicationsFragment : AppNavigatorApplicationsFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub used to resolve the installed applications.</param>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public AppNavigatorApplicationsFragment(IComponentHub componentHub, IFragmentContext fragmentContext)
            : base(componentHub, fragmentContext)
        {
        }
    }
}
