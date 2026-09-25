using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using SavedSearchEntity = KleeneStar.Model.Entities.SavedSearch;

namespace KleeneStar.Core.WebFragment.Search
{
    /// <summary>
    /// Says how a saved search is run and which one the global search page is running.
    /// </summary>
    /// <remarks>
    /// A saved search is run by opening the search page with its id in <see cref="Parameter"/>.
    /// The page resolves the query from the <em>stored</em> record rather than from a copy in
    /// the address, so a link to a saved search runs what it says today, and a caller who may
    /// not see the search (it was never shared with them, or no longer is) gets the plain search
    /// page. An ad-hoc expression can still be handed over in <see cref="WqlParameter"/>; the
    /// saved search wins when both are present.
    /// </remarks>
    public static class SavedSearchRun
    {
        /// <summary>
        /// The query parameter naming the saved search being run.
        /// </summary>
        public const string Parameter = "use";

        /// <summary>
        /// The query parameter carrying a WQL expression to open the page with.
        /// </summary>
        public const string WqlParameter = "wql";

        /// <summary>
        /// Builds the address that runs a saved search.
        /// </summary>
        /// <param name="savedSearch">The saved search to run.</param>
        /// <returns>The run address, or <see langword="null"/> without a search page.</returns>
        public static IUri RunUri(SavedSearchEntity savedSearch)
        {
            return savedSearch is null
                ? null
                : CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>()?
                    .Add(new UriQuery(Parameter, savedSearch.Id.ToString()));
        }

        /// <summary>
        /// Returns the saved search the request runs, when the caller may see it.
        /// </summary>
        /// <param name="request">The request of the search page (or of a dialog opened from it).</param>
        /// <returns>The saved search, or <see langword="null"/>.</returns>
        public static SavedSearchEntity Resolve(IRequest request)
        {
            var value = request?.GetParameter(Parameter)?.Value;

            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            {
                return null;
            }

            var savedSearch = CoreHub.SavedSearchManager.GetSavedSearch(id);

            return SavedSearchAuthorization.MayRead(savedSearch, request) ? savedSearch : null;
        }

        /// <summary>
        /// Returns the WQL expression the search page opens with: the query of the saved search
        /// it runs, else the expression handed over in the address.
        /// </summary>
        /// <param name="request">The request of the search page.</param>
        /// <returns>The expression, or <see langword="null"/> when the page opens without one.</returns>
        public static string ResolveWql(IRequest request)
        {
            var wql = Resolve(request)?.Query ?? request?.GetParameter(WqlParameter)?.Value;

            return string.IsNullOrWhiteSpace(wql) ? null : wql;
        }
    }
}
