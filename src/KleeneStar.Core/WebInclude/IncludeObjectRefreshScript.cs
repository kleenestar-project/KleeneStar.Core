using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script that keeps the view of an object up to date - on its detail page and in the
    /// preview pane of a master-detail view - after a dialog, a workflow transition or another
    /// person has changed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The detail pages are rendered once by the server and the dialogs that write the record
    /// - the edit mask, the prose editor, the security-level and organize dialogs - only close
    /// when they are done, so without this the page went on showing the record as it was when
    /// it was opened. The script listens for the success event a form dispatches and reloads
    /// the page when the form edited the object the page names in its
    /// <see cref="WebFragment.Object.ObjectRefreshFragment"/> meta tag.
    /// </para>
    /// <para>
    /// A workflow transition - an entry of the status menu, or the submit of a transition's
    /// dialog - does not reload the page: the script runs it in the background, fetches the view
    /// with the new state and replaces only the elements whose server markup changed. The same
    /// happens when the data change channel announces that someone else changed the object; the
    /// replaced elements then flash.
    /// </para>
    /// <para>
    /// The include is not scoped: a preview pane is embedded into the overviews, the boards, the
    /// insights and the pages of add-on kinds alike. On a page that names no object and embeds
    /// no preview the script does nothing, so a list page is not reloaded because a row was
    /// edited in a dialog - its table refreshes on its own.
    /// </para>
    /// </remarks>
    [Asset("/assets/js/kleenestar.core.object.refresh.js")]
    public sealed class IncludeObjectRefreshScript : IInclude
    {
    }
}
