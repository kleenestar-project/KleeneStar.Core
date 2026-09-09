using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the object list endpoint of a kind's overview tab control:
    /// the workspace's objects of the <see cref="Kind"/> as a vertical frame list. A
    /// concrete subclass only fixes the kind it lists (issue, asset, …); each concrete
    /// endpoint registers at its own route, so the base must stay abstract (an endpoint
    /// that derived from another endpoint would shadow the base route).
    /// </summary>
    public abstract class RestApiObjectKindList : RestApiList<Model.Entities.Object>
    {
        /// <summary>
        /// Gets the persisted kind key the list is scoped to (e.g.
        /// <see cref="Model.Entities.ObjectKind.Issue"/>).
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Gets the key the quickfilters a user defined for this view are stored under, or
        /// <see langword="null"/> when the view offers none. The bar and this endpoint have to
        /// agree on it.
        /// </summary>
        protected virtual string ViewKey => null;

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the list items: the workspace's objects of the endpoint's kind, with an
        /// object that owns another shown as a tree node above it.
        /// </summary>
        /// <remarks>
        /// The nesting is built inside the page the base handed over rather than across the
        /// whole result, because the base pages the query before this method sees it. An
        /// object whose parent landed on another page therefore shows at the top level rather
        /// than beneath it — it is never lost, only drawn flat. The table, which pages over
        /// roots itself, has no such boundary; see
        /// <see cref="RestApiObjectKindTable"/>.
        /// </remarks>
        /// <param name="query">The query (carries the applied search filter and paging).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The list items, each opening its object detail page when selected.</returns>
        protected override IEnumerable<RestApiListItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var page = CoreHub.ObjectManager.GetObjects(Scope(query, request), context).ToList();

            var children = page
                .Where(x => x.ParentId is not null)
                .GroupBy(x => x.ParentId.Value)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<Model.Entities.Object>)x.ToList());
            var present = page.Select(x => x.Id).ToHashSet();

            return page
                .Where(x => x.ParentId is null || !present.Contains(x.ParentId.Value))
                .Select(x => ToItem(x, children, request, null));
        }

        /// <summary>
        /// Projects an object to a list item, together with the objects nested beneath it.
        /// </summary>
        /// <param name="entity">The object to project.</param>
        /// <param name="children">The objects of the page grouped by their parent.</param>
        /// <param name="request">The request used to resolve the preview address.</param>
        /// <param name="ancestors">The objects already on the path from the root, or null at it.</param>
        /// <returns>The list item.</returns>
        private static RestApiListItem ToItem
        (
            Model.Entities.Object entity,
            IReadOnlyDictionary<Guid, IReadOnlyList<Model.Entities.Object>> children,
            IRequest request,
            HashSet<Guid> ancestors
        )
        {
            // a parent chain that loops back on itself would otherwise recurse forever
            var path = ancestors is null ? new HashSet<Guid>() : new HashSet<Guid>(ancestors);
            path.Add(entity.Id);

            List<Model.Entities.Object> nested = children.TryGetValue(entity.Id, out var found)
                ? [.. found.Where(x => !path.Contains(x.Id))]
                : [];

            return new RestApiListItem()
            {
                Id = entity.Id.ToString(),
                Text = entity.Summary,
                Image = ObjectIcon.Uri(entity),
                // the selection is handed to the master-detail composite rather than
                // written into the frame, so it stays the single owner of the selection.
                // the pane is fed the reduced view rather than the full reading view: the
                // frame embeds a page's main content region, and that region of the reading
                // view is written for a full-width column
                PrimaryAction = new ActionMasterDetail(ListDetailControl.ControlId)
                {
                    Uri = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                        .ResolvePreviewUri(entity)
                        .BindParameters(request),
                    Item = entity.Id.ToString()
                }.ToJson(),
                Children = nested.Count == 0
                    ? null
                    : [.. nested.Select(x => ToItem(x, children, request, path))]
            };
        }

        /// <summary>
        /// Returns how many objects the list holds in total, before paging narrows it, so the
        /// pager offers every page rather than only the one it was handed.
        /// </summary>
        /// <param name="query">The filtered query, without paging applied.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The number of objects in the whole result.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(Scope(query, request), context).Count();
        }

        /// <summary>
        /// Narrows a query to the workspace the request addresses and to the endpoint's kind.
        /// </summary>
        /// <remarks>
        /// The scope is applied here rather than in <see cref="Filter(string, IQuery{Model.Entities.Object}, IRequest)"/>
        /// because the count and the page have to agree on it: a total counted over a wider set
        /// than the page is drawn from promises pages that do not exist.
        /// </remarks>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request naming the workspace.</param>
        /// <returns>The narrowed query.</returns>
        private IQuery<Model.Entities.Object> Scope(IQuery<Model.Entities.Object> query, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? Guid.Empty;

            return query
                .WhereEquals(x => x.WorkspaceId, id)
                .WhereEquals(x => x.Kind, Kind);
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
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            // the same three fields the table searches, so the presentations of one view
            // answer the same term with the same set - a summary-only search made the
            // tile and the list report fewer hits than the table for the same word
            var needle = filter.ToLower();

            return query.Where
            (
                x => (x.Summary ?? string.Empty).ToLower().Contains(needle) ||
                     (x.Key ?? string.Empty).ToLower().Contains(needle) ||
                     (x.Description ?? string.Empty).ToLower().Contains(needle)
            );
        }

        /// <summary>
        /// Applies the quickfilter chips the client reports as active.
        /// </summary>
        /// <remarks>
        /// The bar is shared with the table view of the same tab, so the chips have to mean the
        /// same thing here; without this override the base ignores them and a click answers with
        /// the unfiltered result.
        /// </remarks>
        /// <param name="filters">The quickfilter ids reported as active.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Object> query, IRequest request)
        {
            return ObjectKindQuickfilter.Apply(query, filters, request, ViewKey);
        }
    }
}
