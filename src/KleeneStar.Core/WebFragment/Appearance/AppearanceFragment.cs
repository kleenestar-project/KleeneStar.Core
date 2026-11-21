using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Appearance
{
    /// <summary>
    /// Draws every page of the core application in the appearance its reader chose; the
    /// behaviour is <see cref="AppearanceFragmentBase"/>'s, shared with the portal's fragment.
    /// </summary>
    [Section<SectionBodyPreferences>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class AppearanceFragment : AppearanceFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppearanceFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
