using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Provides a dropdown component for selecting Workspace items, supporting REST API integration, filtering, and URI
    /// generation.
    /// </summary>
    /// <remarks>
    /// The dropdown is personal to the calling identity. With no search text it surfaces the most
    /// recently visited workspaces first, newest first (a visit to any of a workspace's subpages —
    /// its content, an object detail page, its class management — counts), with favorites marked by
    /// a leading star; any favorites that have not been visited recently are appended so they are
    /// never hidden. As soon as a search term is supplied the dropdown switches to a full-text
    /// search across every workspace. When the identity has no bookmarks yet it falls back to the
    /// full list so the dropdown is never empty on first use.
    /// </remarks>
    [Title("Workspace")]
    [Cache]
    public sealed class Dropdown : RestApiDropdown<Workspace>
    {
        /// <summary>
        /// The maximum number of recently visited workspaces shown below the favorites.
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
        /// <returns>
        /// An IQueryContext instance that can be used to execute queries.
        /// </returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// An enumerable collection of dropdown items, each representing a workspace that matches the query. The
        /// collection is empty if no workspaces are found.
        /// </returns>
        protected override IEnumerable<RestApiDropdownItem> RetrieveItems(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            var filter = request?.GetParameter("q")?.Value;

            // when the user is searching, keep the full searchable list across all workspaces
            if (!string.IsNullOrWhiteSpace(filter) && filter != "null")
            {
                return CoreHub.WorkspaceManager?.GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context)
                    .Select(x => ToItem(x, request));
            }

            // otherwise surface the personal shortcuts: recent visits first (newest first),
            // starring those that are favorites, then any favorites not visited recently
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var favorites = CoreHub.WorkspaceManager.GetFavoriteWorkspaces(ownerId);
            var favoriteIds = favorites.Select(x => x.Id).ToHashSet();

            var seen = new HashSet<Guid>();
            var items = new List<RestApiDropdownItem>();

            foreach (var recent in CoreHub.WorkspaceManager.GetRecentWorkspaces(ownerId, MaxRecent))
            {
                if (recent is not null && global::KleeneStar.Core.WebPermission.ContentVisibility.MayRead(recent.Id) && seen.Add(recent.Id))
                {
                    items.Add(ToItem(recent, request, pinned: favoriteIds.Contains(recent.Id)));
                }
            }

            foreach (var favorite in favorites)
            {
                if (favorite is not null && global::KleeneStar.Core.WebPermission.ContentVisibility.MayRead(favorite.Id) && seen.Add(favorite.Id))
                {
                    items.Add(ToItem(favorite, request, pinned: true));
                }
            }

            // fall back to the full list when the identity has no bookmarks yet, so the
            // dropdown is never empty on first use
            if (items.Count == 0)
            {
                return CoreHub.WorkspaceManager?.GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context)
                    .Select(x => ToItem(x, request));
            }

            return items;
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">
        /// A string representing the filter expression to apply. The format and supported
        /// operators depend on the implementation.
        /// </param>
        /// <param name="query">
        /// The query object to which the filter will be applied.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context for resolving
        /// the appropriate REST API URI.
        /// </param>
        /// /// <returns>
        /// A query representing the filtered set of items that match the criteria defined by
        /// the filter statement.
        /// </returns>
        protected override IQuery<Workspace> Filter(string filter, IQuery<Workspace> query, IRequest request)
        {
            if (filter is null || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );
        }

        /// <summary>
        /// Projects a workspace onto a dropdown item, optionally marking it as a pinned favorite.
        /// </summary>
        /// <param name="workspace">The workspace to project. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <param name="pinned">
        /// When <see langword="true"/>, the display text is prefixed with a star to mark the
        /// workspace as a favorite.
        /// </param>
        /// <returns>The dropdown item.</returns>
        private static RestApiDropdownItem ToItem(Workspace workspace, IRequest request, bool pinned = false)
        {
            return new RestApiDropdownItem()
            {
                Id = workspace.Id,
                Text = (pinned ? "★ " : string.Empty) + workspace.Name,
                Image = workspace.Icon?.Uri?.ToString(),
                Uri = GetUri(workspace, request)?.ToString()
            };
        }

        /// <summary>
        /// Gets the URI associated with the specified request and index item.
        /// </summary>
        /// <param name="item">
        /// The index item that provides context for generating the URI. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request for which to retrieve the URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An object representing the URI for the given request and index item, or null if no URI is available.
        /// </returns>
        private static IUri GetUri(Workspace item, IRequest request)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()?
                .BindParameters(new WorkspaceKeyParameter(item?.Key));
        }
    }
}
