using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Tenants
{
    /// <summary>
    /// Represents a REST API table for managing tenants entities, providing data retrieval 
    /// and option generation functionality for tenant records.
    /// </summary>
    [Title("kleenestar.core:setting.tenant.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Tenant>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _avatarFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Delete>();
            _avatarFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Avatar>();
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
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:setting.tenant.column.assigned.workspace"),
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
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Tenant> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.TenantManager.GetTenants(query, context)
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
                            Content = string.Join(", ", x.Workspaces.Select(x => x.Name))
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
        protected override IQuery<Model.Entities.Tenant> Filter(string filter, IQuery<Model.Entities.Tenant> query, IRequest request)
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
        protected override IQuery<Model.Entities.Tenant> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Tenant> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == TenantState.Active);
                        break;
                    default:
                        continue;
                }
            }

            // the filters the user defined are resolved from storage rather than from a case above,
            // and compose onto the query so they combine with the chips handled here
            return CustomQuickfilterSupport.Apply(filters, query, Quickfilter.ViewKey);
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
        private IEnumerable<RestApiOption> GetOptions(Model.Entities.Tenant row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(request)
                .BindParameters(new TenantIdParameter(row.Id));
            var cloneUri = _cloneFormUri?
                .BindParameters(request)
                .BindParameters(new TenantIdParameter(row.Id));
            var deleteUri = _deleteFormUri?
                .BindParameters(request)
                .BindParameters(new TenantIdParameter(row.Id));
            var avatarUri = _avatarFormUri?
                .BindParameters(request)
                .BindParameters(new TenantIdParameter(row.Id));

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

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:setting.tenant.avatar.label"),
                Icon = new IconImage(),
                PrimaryAction = new ActionModal("modal-form", avatarUri, TypeModalSize.ExtraLarge)
            };

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
        private static IUri GetUri(Model.Entities.Tenant row, IRequest request)
        {
            return null;
        }
    }
}
