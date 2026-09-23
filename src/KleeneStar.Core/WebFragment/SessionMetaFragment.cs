using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Tells every page of the core application when its sign-in's access token ends
    /// (see <see cref="SessionMetaFragmentBase"/>).
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class SessionMetaFragment : SessionMetaFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SessionMetaFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
