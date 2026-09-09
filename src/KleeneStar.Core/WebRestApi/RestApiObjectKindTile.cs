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
    /// Project-wide base for the object tile endpoint of a kind's overview tab control:
    /// the workspace's objects of the <see cref="Kind"/> as a card grid. A concrete
    /// subclass only fixes the kind it lists (issue, asset, …); each concrete endpoint
    /// registers at its own route, so the base must stay abstract.
    /// </summary>
    public abstract class RestApiObjectKindTile : RestApiTile<Model.Entities.Object>
    {
        /// <summary>
        /// Gets the persisted kind key the tile view is scoped to.
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
        /// Retrieves the tile items: the workspace's objects of the endpoint's kind.
        /// </summary>
        /// <param name="query">The query (carries the applied search filter and paging).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The tile items, each opening its object detail page when selected.</returns>
        protected override IEnumerable<RestApiTileItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(Scope(query, request), context)
                .Select(x => new RestApiTileItem()
                {
                    Id = x.Id.ToString(),
                    Title = x.Summary,
                    Text = x.Description,
                    Image = ObjectIcon.Uri(x),
                    PrimaryAction = GetPrimaryAction(x, request)?.ToJson()
                });
        }

        /// <summary>
        /// Returns how many objects the tile view holds in total, before paging narrows it, so
        /// the pager offers every page rather than only the one it was handed.
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

        /// <summary>
        /// Retrieves the primary action associated with the specified row item: opening its
        /// object detail page in the object-view frame.
        /// </summary>
        /// <param name="item">The object the action addresses.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The primary action.</returns>
        private static IAction GetPrimaryAction(Model.Entities.Object item, IRequest request)
        {
            return new ActionFrame("object-view-frame")
            {
                Uri = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                        .ResolveDetailUri(item)
            };
        }
    }
}
