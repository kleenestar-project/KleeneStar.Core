using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// The header avatar of the core application: the name and the profile picture of whoever
    /// is signed in. See <see cref="SessionAvatarFragmentBase"/>.
    /// </summary>
    [Section<SectionAppAvatar>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class HeaderAvatarFragment : SessionAvatarFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public HeaderAvatarFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
