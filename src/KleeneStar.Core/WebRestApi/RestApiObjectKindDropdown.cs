using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the per-kind object dropdowns in the application header.
    /// With no search term it surfaces the calling identity's most recently opened
    /// objects <em>of the dropdown's kind</em> (newest first); with a search term it runs
    /// a full-text search across the objects of that kind by summary. A concrete subclass
    /// only fixes the <see cref="Kind"/> it represents (document, blog, issue, asset, …).
    /// </summary>
    public abstract class RestApiObjectKindDropdown : RestApiDropdown<Model.Entities.Object>
    {
        /// <summary>
        /// The maximum number of recently opened objects shown in the dropdown.
        /// </summary>
        private const int MaxRecent = 10;

        /// <summary>
        /// Gets the persisted kind key the dropdown lists (e.g.
        /// <see cref="Model.Entities.ObjectKind.Document"/>). The recents and the search
        /// are scoped to this kind.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the dropdown items: the calling identity's recently opened objects of
        /// the dropdown's kind when no search term is supplied, otherwise the objects of
        /// that kind matching the search term.
        /// </summary>
        /// <param name="query">The query (carries the applied search filter and paging).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The dropdown items, each opening its object detail page when selected.</returns>
        protected override IEnumerable<RestApiDropdownItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var filter = request?.GetParameter("q")?.Value;

            // when the user is searching, search across the objects of this kind (the kind
            // equality is applied in Filter)
            if (!string.IsNullOrWhiteSpace(filter) && filter != "null")
            {
                return CoreHub.ObjectManager?.GetObjects(query, context)
                    .Select(x => ToItem(x, request));
            }

            // otherwise surface the most recently opened objects of this kind, newest first
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return CoreHub.ObjectManager.GetRecentObjects(ownerId, MaxRecent, Kind)
                .Select(x => ToItem(x, request));
        }

        /// <summary>
        /// Applies the search and kind criteria to the given query object.
        /// </summary>
        /// <param name="filter">A string representing the filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>A query representing the filtered set of items.</returns>
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            // the search path is always scoped to the dropdown's kind
            query = query.WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Normalize(Kind));

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
                Image = ObjectIcon.Uri(@object),
                Uri = GetUri(@object, request)?.ToString()
            };
        }

        /// <summary>
        /// Gets the object detail page URI for the supplied object, dispatched by its kind.
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
