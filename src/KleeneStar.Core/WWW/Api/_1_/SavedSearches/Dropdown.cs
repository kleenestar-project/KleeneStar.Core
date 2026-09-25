using KleeneStar.Core.WebFragment.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearches
{
    // The entity type SavedSearch collides with the sibling WWW.SavedSearch namespace;
    // alias it (inside the namespace block) so the bare name binds to the entity.
    using SavedSearch = KleeneStar.Model.Entities.SavedSearch;

    /// <summary>
    /// Provides the entries of the search menu in the application header: the caller's saved
    /// searches (starred first), the ones shared with them, and the searches they ran last.
    /// </summary>
    /// <remarks>
    /// The last searches are the WQL history of the object search
    /// (<see cref="WebManager.IWqlHistoryManager"/>) - the expressions the caller actually ran, not
    /// what they typed - and open the search page on the expression again. A term typed into the
    /// menu's own search box narrows all three groups by name or expression. Headings travel as
    /// payload text, so they are translated here.
    /// </remarks>
    [Title("kleenestar.core:search.dropdown.label")]
    [Cache]
    public sealed class Dropdown : RestApiDropdown<SavedSearch>
    {
        /// <summary>
        /// The most saved searches of the caller's own the menu lists.
        /// </summary>
        private const int MaxSaved = 10;

        /// <summary>
        /// The most saved searches shared with the caller the menu lists.
        /// </summary>
        private const int MaxShared = 5;

        /// <summary>
        /// The most recent searches the menu lists.
        /// </summary>
        private const int MaxRecent = 5;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dropdown()
        {
        }

        /// <summary>
        /// Retrieves the menu entries of the caller.
        /// </summary>
        /// <param name="query">The query (unused - the entries are resolved through the managers).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The entries, grouped under headings; none for somebody not signed in.</returns>
        protected override IEnumerable<RestApiDropdownItem> RetrieveItems(IQuery<SavedSearch> query, IQueryContext context, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return [];
            }

            var term = request?.GetParameter("q")?.Value;
            term = string.IsNullOrWhiteSpace(term) || term == "null" ? null : term.Trim();

            bool matches(string text) => term is null || (text?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

            var own = CoreHub.SavedSearchManager.GetForOwner(identityId)
                .Where(x => matches(x.Name) || matches(x.Query))
                .Take(MaxSaved)
                .Select(x => SavedItem(x, x.Starred));

            var shared = CoreHub.SavedSearchManager.GetSharedWith(identityId)
                .Where(x => matches(x.Name) || matches(x.Query))
                .Take(MaxShared)
                .Select(x => SavedItem(x, false));

            var recent = CoreHub.WqlHistoryManager
                .GetHistory(request, typeof(Model.Entities.Object).FullName)
                .Where(matches)
                .Take(MaxRecent)
                .Select(RecentItem);

            return Group(request, "kleenestar.core:search.dropdown.saved", own)
                .Concat(Group(request, "kleenestar.core:search.dropdown.shared", shared))
                .Concat(Group(request, "kleenestar.core:search.dropdown.recent", recent));
        }

        /// <summary>
        /// Puts a heading over a group of entries - nothing at all when the group is empty.
        /// </summary>
        /// <param name="request">The request, for the culture of the heading.</param>
        /// <param name="heading">The i18n key of the heading.</param>
        /// <param name="items">The entries of the group.</param>
        /// <returns>The heading and the entries.</returns>
        private static IEnumerable<RestApiDropdownItem> Group(IRequest request, string heading, IEnumerable<RestApiDropdownItem> items)
        {
            var list = items.ToList();

            if (list.Count == 0)
            {
                return [];
            }

            return list.Prepend(new RestApiDropdownItemHeader(I18N.Translate(request, heading)));
        }

        /// <summary>
        /// Describes a saved search as an entry that runs it.
        /// </summary>
        /// <param name="savedSearch">The saved search.</param>
        /// <param name="starred">Whether to mark it with the star.</param>
        /// <returns>The entry.</returns>
        private static RestApiDropdownItem SavedItem(SavedSearch savedSearch, bool starred)
        {
            return new RestApiDropdownItem()
            {
                Id = savedSearch.Id,
                Text = (starred ? "★ " : string.Empty) + savedSearch.Name,
                Uri = SavedSearchRun.RunUri(savedSearch)?.ToString()
            };
        }

        /// <summary>
        /// Describes a search the caller ran as an entry that opens the search page on it again.
        /// </summary>
        /// <param name="wql">The expression.</param>
        /// <returns>The entry.</returns>
        private static RestApiDropdownItem RecentItem(string wql)
        {
            return new RestApiDropdownItem()
            {
                // stable per expression, so the client can key the entry
                Id = new Guid(MD5.HashData(Encoding.UTF8.GetBytes(wql))),
                Text = wql,
                Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>()?
                    .Add(new UriQuery(SavedSearchRun.WqlParameter, wql))
                    .ToString()
            };
        }
    }
}
