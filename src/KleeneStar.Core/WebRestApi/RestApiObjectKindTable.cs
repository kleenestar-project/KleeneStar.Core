using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the signatures read
// naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the table endpoint of a kind's overview: the workspace's
    /// objects of the <see cref="Kind"/>, filtered by the search term and the personal
    /// quickfilters (starred, assigned to me, created by me, archived), sorted, and paged.
    /// </summary>
    /// <remarks>
    /// The columns are not fixed. Every field of every class of the kind in the workspace is
    /// offered as a column alongside the object's own properties (see
    /// <see cref="ObjectTableColumnCatalog"/>), and which of them a table shows, in what
    /// order and at what width, is the choice of the user looking at it. That choice is
    /// stored per identity in the <c>UserSession</c> table through
    /// <see cref="WebManager.ISessionManager"/>, and it is stored per view: an overview
    /// hosts several tabs backed by one endpoint, and the <c>v</c> parameter in the address
    /// of each tab's table tells them apart, so two tabs of the same workspace keep separate
    /// column sets.
    ///
    /// Starring is deliberately not a column: it is a mark on the row, so a starred object
    /// shows a star beside its key and is toggled from the row menu.
    ///
    /// The endpoint is a plain <see cref="IRestApi"/> rather than a
    /// <c>RestApiTable&lt;Object&gt;</c> because the starred scope is a per-identity
    /// projection (the favorite flag lives on the caller's visit rows) that a WebIndex query
    /// cannot express — the base class would page the wrong set. Filtering, sorting and
    /// paging therefore run in memory over the workspace's objects. The endpoint honours the
    /// same query parameters the other REST tables use: <c>q</c> (substring search), <c>f</c>
    /// (comma-separated quickfilter ids), <c>p</c> (zero-based page number), <c>l</c> (page
    /// size), <c>o</c> (order column id) and <c>d</c> (order direction).
    ///
    /// A concrete subclass fixes the kind it lists and the view its user-defined
    /// quickfilters are stored under; each concrete endpoint registers at its own route, so
    /// this base must stay abstract. It exists because the issue and asset tables were
    /// written twice and drifted: the asset one never gained the column catalog, which is
    /// the whole point of the view.
    /// </remarks>
    public abstract class RestApiObjectKindTable : IRestApi
    {
        /// <summary>
        /// The default page size used when the request carries no (or an invalid)
        /// <c>l</c> parameter.
        /// </summary>
        private const int DefaultPageSize = 50;

        /// <summary>
        /// The request parameter naming the view whose column layout applies.
        /// </summary>
        private const string ViewParameter = "v";

        /// <summary>The quickfilter id prefix shared by every chip.</summary>
        private const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the starred chip.</summary>
        protected const string StarredId = IdPrefix + "starred";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        protected const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the created-by-me chip.</summary>
        protected const string CreatedId = IdPrefix + "created";

        /// <summary>Quickfilter id of the archived chip.</summary>
        protected const string ArchivedId = IdPrefix + "archived";

        /// <summary>
        /// Gets the persisted kind key the table is scoped to.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Gets the key the quickfilters a user defined for this view are stored under. The
        /// bar and the table have to agree on it.
        /// </summary>
        protected abstract string ViewKey { get; }

        /// <summary>
        /// Builds the row overflow menu. The default is empty, because the routes such a
        /// menu addresses (edit, clone, delete, favorite) exist per kind and a kind without
        /// them must not offer entries that lead nowhere.
        /// </summary>
        /// <param name="entity">The object the options act on.</param>
        /// <param name="starred">Whether the calling identity has starred the object.</param>
        /// <param name="request">The request used to resolve localized labels and URIs.</param>
        /// <returns>The overflow menu options.</returns>
        protected virtual IEnumerable<RestApiOption> GetOptions(ObjectEntity entity, bool starred, IRequest request)
        {
            return [];
        }

        /// <summary>
        /// Builds the filtered, sorted, paged page of the workspace's objects and returns it
        /// as a table result response.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The table result as a JSON response.</returns>
        [Method(RequestMethod.GET)]
        public virtual IResponse Get(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            var pageNumber = Math.Max(0, ParseInt(request, "p", 0));
            var pageSize = ParseInt(request, "l", DefaultPageSize);
            if (pageSize <= 0)
            {
                pageSize = DefaultPageSize;
            }

            var search = request?.GetParameter("q")?.Value;
            var filters = request?.GetParameter("f")?.Value?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
            var selected = new HashSet<string>(filters, StringComparer.OrdinalIgnoreCase);

            var starredIds = CoreHub.ObjectManager.GetFavoriteObjects(ownerId)
                .Select(x => x.Id)
                .ToHashSet();

            var objects = GetObjects(workspace?.Id).AsEnumerable();

            // the archived chip flips the lifecycle scope: without it the table shows the
            // active objects, with it the archived history
            var state = selected.Contains(ArchivedId)
                ? Model.Entities.WorkspaceState.Archived
                : Model.Entities.WorkspaceState.Active;
            objects = objects.Where(x => x.State == state);

            if (selected.Contains(StarredId))
            {
                objects = objects.Where(x => starredIds.Contains(x.Id));
            }

            if (selected.Contains(MineId))
            {
                objects = objects.Where(x => x.AssigneeId == ownerId);
            }

            if (selected.Contains(CreatedId))
            {
                objects = objects.Where(x => x.CreatorId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(search) && search != "null")
            {
                objects = objects.Where(x =>
                    (x.Key ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Summary ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (x.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            // the filters the user defined are resolved from storage rather than from a chip
            // id handled above, and narrow further so they combine with the scopes and the
            // search
            objects = CustomQuickfilterSupport.Apply(filters, objects, ViewKey);

            var catalog = ObjectTableColumnCatalog.Build(workspace?.Id, Kind, request);
            var layout = ResolveLayout(catalog, request);

            var filtered = objects.ToList();

            // an object whose parent survived the same filter is shown nested beneath it, so the
            // containment the data expresses is what the table draws; everything else is a root.
            // paging counts roots, because a page that split a parent from its children would
            // show orphans
            var children = filtered
                .Where(x => x.ParentId is not null)
                .GroupBy(x => x.ParentId.Value)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ObjectEntity>)x.ToList());
            var present = filtered.Select(x => x.Id).ToHashSet();
            var roots = filtered
                .Where(x => x.ParentId is null || !present.Contains(x.ParentId.Value))
                .ToList();

            var page = Sort(roots, layout, request)
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .ToList();

            // the values and class definitions of the whole page are read in one go, so a
            // table with twenty field columns does not issue a query per cell. the nested rows
            // are part of the page, so their values are read with it
            var projection = ObjectTableProjection.Build([.. Flatten(page, children)]);

            var result = new RestApiTableResult()
            {
                Title = null,
                Columns = layout.Select(x => x.Column.ToRestApiColumn(x.Visible, x.Width)),
                Rows = page.Select(x => BuildRow(x, layout, projection, children, starredIds, request)),
                Pagination = new RestApiPaginationInfo()
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = roots.Count
                }
            };

            return result.ToResponse();
        }

        /// <summary>
        /// Handles the column layout the user configured in the table's column manager: the
        /// visible set, their order and their widths, stored against the calling identity
        /// and the addressed view.
        /// </summary>
        /// <remarks>
        /// The client sends the same payload it sends to <c>RestApiTable.Configure</c>
        /// (<c>{ "c": [{ "id", "visible", "width" }, …] }</c>) plus, on a row reorder, a row
        /// id list under <c>r</c>. This table sorts by a column rather than by a stored row
        /// sequence, so the row list is accepted and ignored.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns><c>204</c> once stored, or <c>400</c> for an unreadable payload.</returns>
        [Method(RequestMethod.PUT)]
        public virtual IResponse Configure(IRequest request)
        {
            var payload = ReadConfigurePayload(request);

            if (payload?.Columns is not { Count: > 0 })
            {
                return new ResponseBadRequest(new StatusMessage("Missing column configuration."));
            }

            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
            var catalog = ObjectTableColumnCatalog.Build(workspace?.Id, Kind, request);
            var known = catalog.Columns.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var snapshot = new List<RestApiTableColumn>(known.Count);

            // the client reports the columns it currently holds; an id it does not know
            // (a field deleted meanwhile, or a stale layout) is dropped rather than stored
            foreach (var update in payload.Columns)
            {
                if (string.IsNullOrWhiteSpace(update?.Id) ||
                    !known.TryGetValue(update.Id, out var column) ||
                    !seen.Add(column.Id))
                {
                    continue;
                }

                snapshot.Add(column.ToRestApiColumn(update.Visible ?? column.DefaultVisible, update.Width));
            }

            if (snapshot.Count == 0)
            {
                return new ResponseBadRequest(new StatusMessage("No known column in the configuration."));
            }

            CoreHub.SessionManager.SetTableLayout(request, ResolveLayoutKey(request), snapshot);

            return new ResponseNoContent();
        }

        /// <summary>
        /// Returns the key the calling identity's column layout for the addressed view is
        /// stored under. The view is named by the <c>v</c> parameter; a table addressed
        /// without one (a direct call, or a tab whose view could not be resolved) falls back
        /// to a shared default, so it still remembers a layout rather than none.
        /// </summary>
        /// <remarks>
        /// The key is namespaced by the concrete endpoint type, not by this base, so the
        /// tables of two kinds never share a stored layout — their column catalogs are
        /// different sets.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The layout key.</returns>
        private string ResolveLayoutKey(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value ?? string.Empty;
            var view = request?.GetParameter(ViewParameter)?.Value;

            if (string.IsNullOrWhiteSpace(view) || view == "null")
            {
                view = "default";
            }

            return $"{GetType().FullName}:{workspaceKey}:{view}";
        }

        /// <summary>
        /// Lays the stored per-identity, per-view layout over the catalog: the stored columns
        /// come first in their stored order with their stored visibility and width, and every
        /// column the layout does not mention follows, hidden — a field added to a class
        /// after the user configured the table is offered in the column manager without
        /// forcing itself into the table.
        /// </summary>
        /// <param name="catalog">The columns the table can offer.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The effective columns in display order.</returns>
        private IReadOnlyList<ObjectTableColumnState> ResolveLayout(ObjectTableColumnCatalog catalog, IRequest request)
        {
            var stored = CoreHub.SessionManager.GetTableLayout(request, ResolveLayoutKey(request));

            if (stored is null || stored.Count == 0)
            {
                return
                [
                    .. catalog.Columns.Select(x => new ObjectTableColumnState
                    {
                        Column = x,
                        Visible = x.DefaultVisible
                    })
                ];
            }

            var known = catalog.Columns.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var layout = new List<ObjectTableColumnState>(known.Count);

            foreach (var entry in stored)
            {
                if (string.IsNullOrWhiteSpace(entry?.Id) ||
                    !known.TryGetValue(entry.Id, out var column) ||
                    !seen.Add(column.Id))
                {
                    continue;
                }

                layout.Add(new ObjectTableColumnState
                {
                    Column = column,
                    Visible = entry.Visible ?? column.DefaultVisible,
                    Width = entry.Width
                });
            }

            layout.AddRange(catalog.Columns
                .Where(x => !seen.Contains(x.Id))
                .Select(x => new ObjectTableColumnState
                {
                    Column = x,
                    Visible = false
                }));

            return layout;
        }

        /// <summary>
        /// Orders the objects by the column the client asked for, falling back to the most
        /// recently updated first. The comparison runs over the cell content of the column,
        /// so a table sorts by what it shows.
        /// </summary>
        /// <param name="objects">The filtered objects.</param>
        /// <param name="layout">The effective columns.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The ordered objects.</returns>
        private static IEnumerable<ObjectEntity> Sort
        (
            IReadOnlyList<ObjectEntity> objects,
            IReadOnlyList<ObjectTableColumnState> layout,
            IRequest request
        )
        {
            var orderBy = request?.GetParameter("o")?.Value;
            var descending = string.Equals(request?.GetParameter("d")?.Value, "desc", StringComparison.OrdinalIgnoreCase);

            var column = string.IsNullOrWhiteSpace(orderBy)
                ? null
                : layout.FirstOrDefault(x => string.Equals(x.Column.Id, orderBy, StringComparison.OrdinalIgnoreCase))?.Column;

            if (column is null)
            {
                return objects.OrderByDescending(x => x.Updated);
            }

            // sorting reads the cell content, so the values of every object in scope are
            // needed rather than only those of the page
            var projection = ObjectTableProjection.Build(objects);

            return descending
                ? objects.OrderByDescending(x => column.Read(x, projection), StringComparer.OrdinalIgnoreCase)
                : objects.OrderBy(x => column.Read(x, projection), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Fetches the objects of the kind in the supplied workspace. Returns an empty list
        /// when the workspace is unknown.
        /// </summary>
        /// <param name="workspaceId">The id of the workspace, or <see langword="null"/>.</param>
        /// <returns>The workspace's objects of the kind. The list may be empty.</returns>
        private IReadOnlyList<ObjectEntity> GetObjects(Guid? workspaceId)
        {
            if (workspaceId is null)
            {
                return [];
            }

            var query = new Query<ObjectEntity>()
                .WhereEquals(x => x.WorkspaceId, workspaceId.Value)
                .WhereEquals(x => x.Kind, Kind);

            return [.. CoreHub.ObjectManager.GetObjects(query)];
        }

        /// <summary>
        /// Walks a set of roots and everything nested beneath them, so the values of a page
        /// can be read in one go rather than per level.
        /// </summary>
        /// <param name="roots">The rows the page starts from.</param>
        /// <param name="children">The objects of the result grouped by their parent.</param>
        /// <returns>The roots and their descendants.</returns>
        private static IEnumerable<ObjectEntity> Flatten
        (
            IEnumerable<ObjectEntity> roots,
            IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEntity>> children
        )
        {
            // a parent chain that loops back on itself would otherwise never end; an object
            // already seen is not descended into a second time
            var seen = new HashSet<Guid>();
            var pending = new Stack<ObjectEntity>(roots);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                if (!seen.Add(current.Id))
                {
                    continue;
                }

                yield return current;

                if (children.TryGetValue(current.Id, out var nested))
                {
                    foreach (var child in nested)
                    {
                        pending.Push(child);
                    }
                }
            }
        }

        /// <summary>
        /// Projects a single object to a table row: one cell per column of the effective
        /// layout — including the hidden ones, because the client keeps their content and
        /// shows it the moment the column is switched on — the object endpoint an inline edit
        /// writes through, the link to the detail page, the row menu, and the rows of the
        /// objects nested beneath it.
        /// </summary>
        /// <param name="entity">The object to project.</param>
        /// <param name="layout">The effective columns.</param>
        /// <param name="projection">The loaded class definitions and field values.</param>
        /// <param name="children">The objects of the result grouped by their parent.</param>
        /// <param name="starredIds">The objects the calling identity has starred.</param>
        /// <param name="request">The request used to resolve localized content and URIs.</param>
        /// <param name="ancestors">The objects already on the path from the root, or null at it.</param>
        /// <returns>The table row.</returns>
        private RestApiTableRow BuildRow
        (
            ObjectEntity entity,
            IReadOnlyList<ObjectTableColumnState> layout,
            ObjectTableProjection projection,
            IReadOnlyDictionary<Guid, IReadOnlyList<ObjectEntity>> children,
            IReadOnlySet<Guid> starredIds,
            IRequest request,
            HashSet<Guid> ancestors = null
        )
        {
            // the reading view is addressed through the kind catalog rather than through a
            // route named here, so a kind brings its own detail page without this base
            // knowing it
            var uri = ObjectKindCatalog.ResolveDetailUri(Kind, entity.Key);
            var starred = starredIds.Contains(entity.Id);

            // a parent chain that loops back on itself would otherwise recurse forever
            var path = ancestors is null ? new HashSet<Guid>() : new HashSet<Guid>(ancestors);
            path.Add(entity.Id);

            List<ObjectEntity> nested = children.TryGetValue(entity.Id, out var found)
                ? [.. found.Where(x => !path.Contains(x.Id))]
                : [];

            return new RestApiTableRow()
            {
                Id = entity.Id.ToString(),
                Cells = [.. layout.Select(x => new RestApiTableCell()
                {
                    Content = x.Column.Read?.Invoke(entity, projection)
                })],
                Options = GetOptions(entity, starred, request).Select(o => o.ToJson()),
                Bind = BuildRowBinding(entity, layout, projection),
                Uri = uri?.ToString(),
                RestApi = ResolveObjectRestUri(entity, request)?.ToString(),
                // a starred object is marked rather than given a column of its own; the star
                // sits beside the object icon in the row's leading cell
                Icon = starred ? "fas fa-star" : null,
                Image = ObjectIcon.Uri(entity),
                // the nested rows are ordered by the same column the roots are, so one sort
                // governs the whole table rather than only its top level
                Children = nested.Count == 0
                    ? null
                    : [.. Sort(nested, layout, request)
                        .Select(x => BuildRow(x, layout, projection, children, starredIds, request, path))]
            };
        }

        /// <summary>
        /// Names the columns this row cannot be edited in, so the cell renderer offers no
        /// editor there.
        /// </summary>
        /// <remarks>
        /// A field column folds the same-named fields of every class of the kind in the
        /// workspace, but a class-specific field exists on one class only. An object of
        /// another class has nowhere to put such a value, and an edit of it would be dropped
        /// on save and silently revert on the next query. The row therefore reports those
        /// columns, by the payload name their editor would write, and the renderer draws them
        /// read-only.
        /// </remarks>
        /// <param name="entity">The object the row shows.</param>
        /// <param name="layout">The effective columns.</param>
        /// <param name="projection">The loaded class definitions and field values.</param>
        /// <returns>The row binding payload.</returns>
        private static IDictionary<string, object> BuildRowBinding
        (
            ObjectEntity entity,
            IReadOnlyList<ObjectTableColumnState> layout,
            ObjectTableProjection projection
        )
        {
            var blocked = layout
                .Select(x => x.Column)
                .Where(x => x.FieldIds.Count > 0 && !string.IsNullOrEmpty(x.Name))
                .Where(x => !projection.DefinesField(entity, x.FieldIds))
                .Select(x => x.Name);

            return new Dictionary<string, object>
            {
                ["readonly"] = string.Join(",", blocked)
            };
        }

        /// <summary>
        /// Returns the object CRUD endpoint addressed at the supplied object, which is what
        /// an inline cell edit PUTs its <c>{ name: value }</c> payload to.
        /// </summary>
        /// <param name="entity">The object the row shows.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The bound endpoint address.</returns>
        private static IUri ResolveObjectRestUri(ObjectEntity entity, IRequest request)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            return uri?
                .Add(new UriQuery("id", entity.Id.ToString()))
                .BindParameters(request);
        }

        /// <summary>
        /// Reads the column configuration payload of a <c>PUT</c>.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The payload, or <see langword="null"/> when the body carries none.</returns>
        private static RestApiTableConfigurePayload ReadConfigurePayload(IRequest request)
        {
            if (request is not Request raw ||
                raw.Content is not { Length: > 0 } content ||
                raw.Header?.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) != true)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RestApiTableConfigurePayload>(content);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses an integer request parameter, falling back to a default when the parameter
        /// is missing or not a number.
        /// </summary>
        /// <param name="request">The request carrying the parameter.</param>
        /// <param name="name">The parameter name.</param>
        /// <param name="fallback">The value returned when parsing fails.</param>
        /// <returns>The parsed value, or <paramref name="fallback"/>.</returns>
        private static int ParseInt(IRequest request, string name, int fallback)
        {
            var raw = request?.GetParameter(name)?.Value;

            return int.TryParse(raw, out var value) ? value : fallback;
        }
    }
}
