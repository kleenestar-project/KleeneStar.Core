using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// Provides the suggestions for the global search box in the application header: with no
    /// search term the calling identity's most recently opened objects across every kind
    /// (newest first); with a search term a full-text search across every object by summary.
    /// Both are titled by a heading that rides in the same stream, so the user can tell the
    /// offered entries from the found ones.
    /// The per-kind header dropdowns are backed by the kind-specific dropdown endpoints
    /// (<see cref="Documents.Dropdown"/>, <see cref="Blogs.Dropdown"/>,
    /// <see cref="Issues.Dropdown"/>, <see cref="Assets.Dropdown"/>).
    /// </summary>
    [Title("kleenestar.core:object.dropdown.label")]
    [Cache]
    public sealed class Dropdown : RestApiDropdown<Model.Entities.Object>
    {
        /// <summary>
        /// The maximum number of recently opened objects shown in the dropdown.
        /// </summary>
        private const int MaxRecent = 10;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dropdown()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Retrieves the dropdown items: the calling identity's recently opened objects when no
        /// search term is supplied, otherwise the objects matching the search term.
        /// </summary>
        /// <param name="query">The query (carries the applied search filter and paging).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The dropdown items, each opening its object detail page when selected.</returns>
        protected override IEnumerable<RestApiDropdownItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var filter = request?.GetParameter("q")?.Value;

            // when the user is searching, search across all objects
            if (!string.IsNullOrWhiteSpace(filter) && filter != "null")
            {
                var matches = CoreHub.ObjectManager?.GetObjects(query, context)
                    .Select(x => ToItem(x, request)) ?? [];

                return Titled("kleenestar.core:search.header.results", matches, request);
            }

            // otherwise surface the most recently opened objects, newest first
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            var recent = CoreHub.ObjectManager.GetRecentObjects(ownerId, MaxRecent)
                .Select(x => ToItem(x, request));

            return Titled("kleenestar.core:search.header.recent", recent, request);
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">A string representing the filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>A query representing the filtered set of items.</returns>
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            if (filter is null || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Summary, filter
            );
        }

        /// <summary>
        /// Prepends a heading to a set of items. An empty set stays empty, so an empty search
        /// result shows the empty state of the search box rather than a lone caption.
        /// </summary>
        /// <param name="key">The internationalization key of the heading.</param>
        /// <param name="items">The items to title.</param>
        /// <param name="request">The request that provides the language.</param>
        /// <returns>The titled items.</returns>
        private static IEnumerable<RestApiDropdownItem> Titled(string key, IEnumerable<RestApiDropdownItem> items, IRequest request)
        {
            var titled = false;

            foreach (var item in items)
            {
                if (!titled)
                {
                    titled = true;

                    yield return new RestApiDropdownItemHeader(I18N.Translate(request, key));
                }

                yield return item;
            }
        }

        /// <summary>
        /// Projects an object onto a dropdown item.
        /// </summary>
        /// <param name="object">The object to project. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The dropdown item.</returns>
        private static RestApiDropdownItem ToItem(Model.Entities.Object @object, IRequest request)
        {
            return new RestApiDropdownItem()
            {
                Id = @object.Id,
                Text = @object.Summary,
                Image = @object.Icon?.Uri?.ToString(),
                Uri = GetUri(@object, request)?.ToString()
            };
        }

        /// <summary>
        /// Gets the object detail page URI for the supplied object.
        /// </summary>
        /// <param name="object">The object that provides context for generating the URI. Cannot be null.</param>
        /// <param name="request">The request for which to retrieve the URI.</param>
        /// <returns>The object detail URI, or null if none is available.</returns>
        private static IUri GetUri(Model.Entities.Object @object, IRequest request)
        {
            return global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                .ResolveDetailUri(@object);
        }
    }
}
