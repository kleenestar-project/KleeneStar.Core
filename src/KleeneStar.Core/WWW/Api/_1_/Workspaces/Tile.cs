using KleeneStar.Core.WebIcon;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WWW.Workspaces._workspacekey_;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Represents a REST API table for managing workspace entities, providing data retrieval 
    /// and option generation functionality for workspace records.
    /// </summary>
    [Title("kleenestar.core:workspace.tile.header")]
    [Cache]
    public sealed class Tile : RestApiTile<Workspace>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _permissionsFormUri;
        private readonly IUri _deleteFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tile()
        {
            _editFormUri = CoreHub.GetUri<Edit>();
            _cloneFormUri = CoreHub.GetUri<Clone>();
            _permissionsFormUri = CoreHub.GetUri<Permissions>();
            _deleteFormUri = CoreHub.GetUri<Delete>();
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
        /// Retrieves a collection of tile items representing classes that match the 
        /// specified query and workspace context.
        /// </summary>
        /// <param name="query">
        /// The query used to filter classes. The query is further constrained to the 
        /// workspace identified by the request parameters.
        /// </param>
        /// <param name="context">
        /// The context for the query execution, providing additional information or 
        /// services required to process the query.
        /// </param>
        /// <param name="request">
        /// The current API request, used to extract workspace identification 
        /// parameters.
        /// </param>
        /// <returns>
        /// An enumerable collection of tile items representing the classes that 
        /// satisfy the query and belong to the specified workspace. The collection 
        /// is empty if no matching classes are found.
        /// </returns>
        protected override IEnumerable<RestApiTileItem> RetrieveItems(IQuery<Model.Entities.Workspace> query, IQueryContext context, IRequest request)
        {
            return CoreHub.WorkspaceManager.GetWorkspaces(query, context)
                .Select(x => new RestApiTileItem()
                {
                    Id = x.Id.ToString(),
                    Title = x.Name,
                    Text = $"{ProseText.ToPlainText(x.Description)}\n{x.AccessModifier} | {I18N.Translate(request, x.Sealed ? "kleenestar.core:workspace.state.sealed" : "kleenestar.core:workspace.state.open")}",
                    Image = x.Icon?.Uri?.ToString()
                    //Options = GetOptions(x, request)
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
                Icon = new ClassIcon()

            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }
    }
}
