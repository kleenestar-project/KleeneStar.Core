using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script that reloads an object detail page after a dialog has changed the object
    /// it shows.
    /// </summary>
    /// <remarks>
    /// The detail pages are rendered once by the server and the dialogs that write the record
    /// - the edit mask, the prose editor, the security-level and organize dialogs - only close
    /// when they are done, so without this the page went on showing the record as it was when
    /// it was opened. The script listens for the success event a form dispatches and reloads
    /// the page when the form edited the object the page names in its
    /// <see cref="WebFragment.Object.ObjectRefreshFragment"/> meta tag; it is scoped to the
    /// detail pages, because that is the only place a page is about one record.
    /// </remarks>
    [Asset("/assets/js/objectrefresh.js")]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    public sealed class IncludeObjectRefreshScript : IInclude
    {
    }
}
