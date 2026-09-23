using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the script that renews a sign-in's access token before it ends, on every page of
    /// the core application. It does nothing on a page without the deadline
    /// <see cref="WebFragment.SessionMetaFragmentBase"/> writes, which is every page an
    /// anonymous caller sees.
    /// </summary>
    [Asset("/assets/js/sessionrefresh.js")]
    public sealed class IncludeSessionRefreshScript : IInclude
    {
    }
}
