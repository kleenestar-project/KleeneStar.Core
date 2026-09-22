using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebApp.WebControl;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.NavigatorLinks
{
    /// <summary>
    /// Represents a REST API table for managing navigator link entities, providing data retrieval
    /// and option generation functionality for navigator link records.
    /// </summary>
    [Title("kleenestar.core:setting.navigatorlink.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<NavigatorLink>
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
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.NavigatorLink._navigatorlinkid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.NavigatorLink._navigatorlinkid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.NavigatorLink._navigatorlinkid_.Delete>();
            _moveUri = CoreHub.GetUri<Move>();
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
        /// An enumerable collection of columns associated with the specified request.
        /// </returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "name",
                Label = I18N.Translate(request, "kleenestar.core:setting.navigatorlink.name.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "uri",
                Label = I18N.Translate(request, "kleenestar.core:setting.navigatorlink.uri.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:setting.navigatorlink.description.label"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "ordinal",
                Label = I18N.Translate(request, "kleenestar.core:setting.navigatorlink.ordinal.label"),
                // the order is already conveyed by the row order, so the raw value stays available
                // for inspection but does not occupy a column by default
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:setting.navigatorlink.state.label"),
                Visible = true,
                // the state is rendered as a chip whose caption and color come from the same
                // selection endpoint the form uses, so both stay in step; the cell therefore
                // carries the id of the state rather than its translated name
                Template = new RestApiTableColumnTemplateRestSelection(false)
                {
                    Uri = CoreHub.GetUri<State>()
                }
            };
        }

        /// <summary>
        /// Retrieves a collection of table rows that match the specified query and context.
        /// </summary>
        /// <param name="query">
        /// The query that defines the criteria for selecting table rows.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed.
        /// </param>
        /// <param name="columns">
        /// The collection of columns to include in the result set.
        /// </param>
        /// <param name="request">
        /// The request object containing metadata or parameters relevant to the retrieval operation.
        /// </param>
        /// <returns>
        /// An enumerable collection of table rows that satisfy the query and context.
        /// </returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<NavigatorLink> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.NavigatorLinkManager.GetNavigatorLinks(query, context)
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Name)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                            Content = x.Name
                        },
                        new() {
                            Content = x.Uri
                        },
                        new() {
                            Content = ProseText.ToPlainText(x.Description)
                        },
                        new() {
                            Content = x.Ordinal.ToString()
                        },
                        new() {
                            Content = x.State.Id().ToString()
                        }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = null
                });
        }

        /// <summary>
        /// Persists the row order the user arranged by dragging a row.
        /// </summary>
        /// <remarks>
        /// The client sends the identifiers of the rows it currently shows, which may be a single
        /// page of a filtered list, so the manager folds them into the global order rather than
        /// treating them as the complete arrangement.
        /// </remarks>
        /// <param name="rowIds">The row identifiers in the order chosen by the user.</param>
        /// <param name="request">The triggering request.</param>
        protected override void UpdateRows(IEnumerable<string> rowIds, IRequest request)
        {
            var ordered = (rowIds ?? [])
                .Select(x => Guid.TryParse(x, out var id) ? (Guid?)id : null)
                .Where(x => x.HasValue)
                .Select(x => x.Value);

            CoreHub.NavigatorLinkManager.Reorder(ordered);
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">
        /// A string representing the filter expression to apply.
        /// </param>
        /// <param name="query">
        /// The query object to which the filter will be applied.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// A query representing the filtered set of items.
        /// </returns>
        protected override IQuery<NavigatorLink> Filter(string filter, IQuery<NavigatorLink> query, IRequest request)
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
        /// Applies the specified quickfilter criteria to the given query object.
        /// </summary>
        /// <param name="filters">
        /// A collection of quickfilter identifiers that should be applied.
        /// </param>
        /// <param name="query">
        /// The query object to which the filter will be applied.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// A query representing the filtered set of items.
        /// </returns>
        protected override IQuery<NavigatorLink> Filter(IEnumerable<string> filters, IQuery<NavigatorLink> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == NavigatorLinkState.Active);
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
        private IUri MoveUri(NavigatorLink row, string direction)
        {
            // Add mutates the instance it is called on, so the cached uri must not be used
            // directly: every option would otherwise append to the same accumulating query
            return _moveUri is null ? null : new UriEndpoint(_moveUri).Add
            (
                new UriQuery("id", row.Id.ToString()),
                new UriQuery("direction", direction)
            );
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
        private IEnumerable<RestApiOption> GetOptions(NavigatorLink row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new NavigatorLinkIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new NavigatorLinkIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new NavigatorLinkIdParameter(row.Id));

            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionClone(request)
            {
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
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }
    }
}
