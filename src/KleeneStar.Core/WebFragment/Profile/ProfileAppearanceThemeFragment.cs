using KleeneStar.Core.WebFragment.Appearance;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The first card of the core's appearance page: theme and mode
    /// (<see cref="AppearanceThemeCardFragmentBase"/>).
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Appearance>]
    [Order(10)]
    [Cache]
    public sealed class ProfileAppearanceThemeFragment : AppearanceThemeCardFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileAppearanceThemeFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
