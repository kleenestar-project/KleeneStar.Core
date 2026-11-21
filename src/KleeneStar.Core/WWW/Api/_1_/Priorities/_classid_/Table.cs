using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Priorities._classid_
{
    /// <summary>
    /// Represents a REST API table for managing priority entities, providing data retrieval 
    /// and option generation functionality for priority records.
    /// </summary>
    [Title("kleenestar.core:priority.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Priority>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _moveUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Priority._priorityid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Priority._priorityid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Priority._priorityid_.Delete>();
            _moveUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Priorities.Move>();
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
        /// Retrieves the collection of columns for the specified REST API request.
        /// </summary>
        /// <param name="request">
        /// The request for which to retrieve the table columns. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of columns associated with the specified request. The 
        /// collection may be empty if no columns are available.
        /// </returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = "Name",
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = "Description",
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = "State",
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves a collection of table rows that match the specified query 
        /// and context.
        /// </summary>
        /// <param name="query">
        /// The query that defines the criteria for selecting table rows.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed, providing additional 
        /// information or constraints.
        /// </param>
        /// <param name="columns">
        /// The collection of columns to include in the result set. Only the specified 
        /// columns will be present in the returned rows.
        /// </param>
        /// <param name="request">
        /// The request object containing metadata or parameters relevant to the 
        /// retrieval operation.
        /// </param>
        /// <returns>
        /// An enumerable collection of table rows that satisfy the query and context. 
        /// The collection may be empty if no rows match the criteria.
        /// </returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Priority> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var classId = request.GetParameter<ClassIdParameter>();
            var guid = Guid.TryParse(classId?.Value, out Guid id) ? id : Guid.Empty;

            query = query.WhereEquals(x => x.ClassId, guid);

            return CoreHub.PriorityManager.GetPriorities(query, context)
                .OrderBy(x => x.Order)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                             Content = x.Name
                        },
                        new() {
                            Content = ProseText.ToPlainText(x.Description)
                        },
                        new() {
                            Content = x.State.ToString()
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString()
                });
        }

        /// <summary>
        /// Persists the user-defined row order produced by drag-and-drop in the UI.
        /// The position of each id becomes its new <see cref="Model.Entities.Priority.Order"/>.
        /// </summary>
        /// <param name="rowIds">The priority ids in the order chosen by the user.</param>
        /// <param name="request">The triggering request.</param>
        protected override void UpdateRows(IEnumerable<string> rowIds, IRequest request)
        {
            var ordered = rowIds?
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();

            if (ordered is null || ordered.Count == 0)
            {
                return;
            }

            CoreHub.PriorityManager.Reorder(ordered);
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
        /// <returns>
        /// A query representing the filtered set of items that match the criteria defined by 
        /// the filter statement.
        /// </returns>
        protected override IQuery<Model.Entities.Priority> Filter(string filter, IQuery<Model.Entities.Priority> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filters">
        /// A collection of quickfilter identifiers that should be applied in addition to the WQL criteria.
        /// </param>
        /// <param name="query">
        /// The query object to which the filter will be applied.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context for resolving
        /// the appropriate REST API URI.
        /// </param>
        /// <returns>
        /// A query representing the filtered set of items that match the criteria defined by 
        /// the filter statement.
        /// </returns>
        protected override IQuery<Model.Entities.Priority> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Priority> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == PriorityState.Active);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Retrieves a collection of options.
        /// </summary>
        /// <param name="row">
        /// The row object for which options are being retrieved. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request object containing the criteria for retrieving options. Cannot be null.
        /// </param>
        /// <summary>
        /// Builds the address that moves the specified priority in the given direction.
        /// </summary>
        /// <remarks>
        /// Add mutates the instance it is called on, so the cached uri must not be used directly:
        /// every option would otherwise append to the same accumulating query.
        /// </remarks>
        /// <param name="row">The priority the option belongs to.</param>
        /// <param name="direction">The direction to move in.</param>
        /// <returns>The address, or null when the endpoint is unavailable.</returns>
        private IUri MoveUri(Model.Entities.Priority row, string direction)
        {
            return _moveUri is null ? null : new UriEndpoint(_moveUri).Add
            (
                new UriQuery("id", row.Id.ToString()),
                new UriQuery("direction", direction)
            );
        }

        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Priority row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new PriorityIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new PriorityIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new PriorityIdParameter(row.Id));


            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                Icon = new IconPen(),
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionClone(request)
            {
                Icon = new IconClone(),
                PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionSeparator(request);

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:order.move.up.label"),
                Icon = new IconArrowUp(),
                PrimaryAction = new ActionRequest(MoveUri(row, "up"), "PUT")
            };

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:order.move.down.label"),
                Icon = new IconArrowDown(),
                PrimaryAction = new ActionRequest(MoveUri(row, "down"), "PUT")
            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                Icon = new IconTrash(),
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }

        /// <summary>
        /// Retrieves a URI that represents the specified request within the given workspace context.
        /// </summary>
        /// <param name="row">
        /// The workspace context in which the request is evaluated. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request for which to obtain the corresponding URI. Cannot be null.
        /// </param>
        /// <returns>
        /// An object implementing <see cref="IUri"/> that represents the URI for the specified request and workspace.
        /// </returns>
        private static IUri GetUri(Model.Entities.Priority row, IRequest request)
        {
            return null;
        }

        ///// <summary>
        ///// Returns the REST API endpoint URI associated with the specified request and workspace.
        ///// </summary>
        ///// <param name="row">
        ///// The workspace context used to determine the appropriate REST API endpoint.
        ///// </param>
        ///// <param name="request">
        ///// The request for which to retrieve the REST API endpoint.
        ///// </param>
        ///// <returns>
        ///// An object representing the URI of the REST API endpoint for the given request and workspace.
        ///// </returns>
        //public override IUri GetRestApiForInlineEdit(Priority row, IRequest request)
        //{
        //    return CoreHub.GetUri<Index>()?
        //        .Add(new UriQuery("id", row.Id.ToString()));
        //}
    }
}
