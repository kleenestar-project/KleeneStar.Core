using KleeneStar.Core.WebFragment.Appearance;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The second card of the core's appearance page: the adjustments over
    /// <c>/api/1/appearance</c> (<see cref="AppearanceFormFragmentBase{TEndpoint}"/>).
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Appearance>]
    [Order(20)]
    [Cache]
    public sealed class ProfileAppearanceFormFragment : AppearanceFormFragmentBase<global::KleeneStar.Core.WWW.Api._1_.Appearance.Index>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileAppearanceFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
