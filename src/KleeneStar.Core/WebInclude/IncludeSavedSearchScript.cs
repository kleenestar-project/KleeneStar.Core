using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script of the global search page that hands the query on screen to the save dialog
    /// and reloads the page after a saved-search dialog changed what the sidebar and the
    /// headline show.
    /// </summary>
    /// <remarks>
    /// Scoped to the search page, because that is the only page whose dialogs are saved-search
    /// dialogs - the reload after any dialog success is safe only there. See
    /// <see cref="WebControl.SavedSearchAdvancedSearch"/> for the attribute it reads.
    /// </remarks>
    [Asset("/assets/js/kleenestar.core.search.saved.js")]
    [Scope<global::KleeneStar.Core.WWW.Search.Index>]
    public sealed class IncludeSavedSearchScript : IInclude
    {
    }
}
