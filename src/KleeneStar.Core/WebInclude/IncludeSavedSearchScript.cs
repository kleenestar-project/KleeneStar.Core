using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script that makes the global search page save and run searches: it opens the
    /// advanced search on the query of the saved search the page runs, hands the query on
    /// screen to the save dialog, and reloads the page after a saved-search dialog changed
    /// what the sidebar and the headline show.
    /// </summary>
    /// <remarks>
    /// Scoped to the search page, because that is the only page whose dialogs are saved-search
    /// dialogs - the reload after any dialog success is safe only there. See
    /// <see cref="WebControl.SavedSearchAdvancedSearch"/> for the attributes it reads.
    /// </remarks>
    [Asset("/assets/js/savedsearch.js")]
    [Scope<global::KleeneStar.Core.WWW.Search.Index>]
    public sealed class IncludeSavedSearchScript : IInclude
    {
    }
}
