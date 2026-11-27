using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script that brings an object detail page up to date after a dialog or a workflow
    /// transition has changed the object it shows.
    /// </summary>
    /// <remarks>
    /// The detail pages are rendered once by the server and the dialogs that write the record
    /// - the edit mask, the prose editor, the security-level and organize dialogs - only close
    /// when they are done, so without this the page went on showing the record as it was when
    /// it was opened. The script listens for the success event a form dispatches and reloads
    /// the page when the form edited the object the page names in its
    /// <see cref="WebFragment.Object.ObjectRefreshFragment"/> meta tag; it is scoped to the
    /// detail pages, because that is the only place a page is about one record.
    /// A workflow transition - an entry of the status menu, or the submit of a transition's
    /// dialog - does not reload the page: the script runs it in the background, fetches the page
    /// with the new state and replaces only the elements whose server markup changed.
    /// </remarks>
    [Asset("/assets/js/kleenestar.core.object.refresh.js")]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindDetail>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    public sealed class IncludeObjectRefreshScript : IInclude
    {
    }
}
