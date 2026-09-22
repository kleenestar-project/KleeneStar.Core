using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Represents a REST API table for managing workspace entities, providing data retrieval 
    /// and option generation functionality for workspace records.
    /// </summary>
    [Title("kleenestar.core:workspace.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Workspace>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _permissionsFormUri;
        private readonly IUri _deleteFormUri;
        private readonly IUri _favoriteUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Clone>();
            _permissionsFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Permissions>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Delete>();
            _favoriteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Favorite>();
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
                Id = "name",
                Label = "Name",
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "category",
                Label = "Category",
                Visible = false,
                Template = new RestApiTableColumnTemplateTag()
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

            yield return new RestApiTableColumn()
            {
                Id = "inherited",
                Label = "Inherited",
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "sealed",
                Label = "Sealed",
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "accessmodifier",
                Label = "Access Modifier",
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "tenant",
                Label = "Tenant",
                Visible = false,
                Template = new RestApiTableColumnTemplateTag()
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
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Workspace> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.WorkspaceManager.GetWorkspaces(query, context)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() {
                             Content = x.Key
                        },
                        new() {
                            Content = x.Name
                        },
                        new() {
                            Content = string.Join(";", x.Categories)
                        },
                        new() {
                            Content = ProseText.ToPlainText(x.Description)
                        },
                        new() {
                            Content = x.State.ToString()
                        },
                        new() {
                            Content = x.Inherited?.Name
                        },
                        new() {
                            Content = x.Sealed.ToString()
                        },
                        new() {
                            Content = x.AccessModifier.ToString()
                        },
                        new() {
                            Content = x.Tenants != null ? string.Join(";", x.Tenants.Select(t => t.Name)) : string.Empty
                        }
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
        protected override IQuery<Workspace> Filter(string filter, IQuery<Workspace> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            query = query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );

            return query;
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
        protected override IQuery<Workspace> Filter(IEnumerable<string> filters, IQuery<Workspace> query, IRequest request)
        {
            foreach (var guids in filters
                .Where(f => f.StartsWith("cat-", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Substring(4))
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g.Value))
            {
                query = query.Where(w => w.Categories.Any(c => c.Id == guids));
            }

            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                var key = filter[3..];

                switch (key.ToLowerInvariant())
                {
                    case "active":
                        query = query.Where(x => x.State == WorkspaceState.Active);
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
        private IEnumerable<RestApiOption> GetOptions(Workspace row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));
            var cloneUri = _cloneFormUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));
            var permissionsUri = _permissionsFormUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));
            var deleteUri = _deleteFormUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));
            var favoriteUri = _favoriteUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var isFavorite = CoreHub.WorkspaceManager.IsFavorite(ownerId, row.Id);


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

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:workspace.permissions.label"),
                Icon = new IconUserShield(),
                PrimaryAction = new ActionModal("modal-form", permissionsUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionCustom(request)
            {
                Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>()?
                    .BindParameters
                    (
                        new WorkspaceKeyParameter(row.Key)
                    ),
                Text = I18N.Translate(request, "kleenestar.core:class.manage.label"),
                Icon = new IconClass()
            };

            // toggle the calling identity's favorite flag; the label reflects the current
            // state and the link flips it, redirecting back to the refreshed list
            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, isFavorite
                    ? "kleenestar.core:workspace.favorite.remove.label"
                    : "kleenestar.core:workspace.favorite.add.label"),
                Icon = new IconStar(),
                Uri = favoriteUri
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
        private static IUri GetUri(Workspace row, IRequest request)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()?
                .BindParameters(new WorkspaceKeyParameter(row.Key));
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
        private static IUri GetRestApiForInlineEdit(Workspace row, IRequest request)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Index>()?
                .Add(new UriQuery("id", row.Id.ToString()));
        }

        /// <summary>
        /// Retrieves the primary action associated with the specified 
        /// workspace and request.
        /// </summary>
        /// <param name="row">
        /// The workspace instance that provides the context for determining 
        /// the primary action.
        /// </param>
        /// <param name="request">
        /// The request object that may influence the selection of the 
        /// primary action.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IAction"/> representing the primary 
        /// action for the given workspace and request.
        /// </returns>
        private static IAction GetPrimaryAction(Workspace row, IRequest request)
        {
            return null;
        }

        /// <summary>
        /// Retrieves the secondary action associated with the specified 
        /// workspace and request.
        /// </summary>
        /// <param name="row">
        /// The workspace instance that provides the context for determining 
        /// the primary action.
        /// </param>
        /// <param name="request">
        /// The request object that may influence the selection of the 
        /// primary action.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IAction"/> representing the primary 
        /// action for the given workspace and request.
        /// </returns>
        private IAction GetSecondaryAction(Workspace row, IRequest request)
        {
            var editUri = _editFormUri?
                .BindParameters(new WorkspaceKeyParameter(row.Key));

            return new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge);
        }
    }
}
