using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Represents a REST API table for managing class entities, providing data retrieval 
    /// and option generation functionality for class records.
    /// </summary>
    [Title("kleenestar.core:object.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Object>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            // this table lists the issue kind only (see RetrieveRows), so the edit / clone /
            // delete modals target the issue action pages that host the generic object CRUD
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Delete>();
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
                Id = "key",
                Label = "Key",
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "summary",
                Label = "Summary",
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = "Description",
                Visible = true
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
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Object> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? System.Guid.Empty;

            // the tab views live on the issue overview, so they present the issue kind only
            query = query
                .WhereEquals(x => x.WorkspaceId, id)
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Issue);

            return CoreHub.ObjectManager.GetObjects(query, context)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                             Content = x.Key
                        },
                        new() {
                            Content = x.Summary
                        },
                        new() {
                            Content = ProseText.ToPlainText(x.Description)
                        },
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString(),
                    Image = x.Icon?.Uri?.ToString()
                });
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
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Summary, filter
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
        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Object row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new ObjectKeyParameter(row.Key));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new ObjectKeyParameter(row.Key));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new ObjectKeyParameter(row.Key));

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

            // extended options

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
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
        private static IUri GetUri(Model.Entities.Object row, IRequest request)
        {
            return global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                .ResolveDetailUri(row)?
                .BindParameters(request);
        }

        /// <summary>
        /// Returns the REST API endpoint URI associated with the specified request and workspace.
        /// </summary>
        /// <param name="row">
        /// The workspace context used to determine the appropriate REST API endpoint.
        /// </param>
        /// <param name="request">
        /// The request for which to retrieve the REST API endpoint.
        /// </param>
        /// <returns>
        /// An object representing the URI of the REST API endpoint for the given request and workspace.
        /// </returns>
        private static IUri GetRestApiForInlineEdit(Model.Entities.Object row, IRequest request)
        {
            return CoreHub.GetUri<Index>()?
                .Add(new UriQuery("id", row.Id.ToString()));
        }
    }
}
