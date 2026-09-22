using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_
{
    /// <summary>
    /// Represents a REST API table for managing class entities, providing data retrieval 
    /// and option generation functionality for class records.
    /// </summary>
    [Title("kleenestar.core:class.tile.header")]
    [Cache]
    public sealed class Tile : RestApiTile<Model.Entities.Class>
    {
        private readonly IUri _editFormUri;
        private readonly IUri _cloneFormUri;
        private readonly IUri _deleteFormUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tile()
        {
            _editFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Edit>();
            _cloneFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Clone>();
            _deleteFormUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Delete>();
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
        protected override IEnumerable<RestApiTileItem> RetrieveItems(IQuery<Model.Entities.Class> query, IQueryContext context, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
            var id = workspace?.Id ?? Guid.Empty;

            query = query.WhereEquals(x => x.WorkspaceId, id);

            return CoreHub.ClassManager.GetClasses(query, context)
                .Select(x => new RestApiTileItem()
                {
                    Id = x.Id.ToString(),
                    Title = x.Name,
                    Text = ProseText.ToPlainText(x.Description),
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
        protected override IQuery<Model.Entities.Class> Filter(string filter, IQuery<Model.Entities.Class> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            query = query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );

            //if (request.GetParameter<CategoryParameter>() is Parameter category)
            //{
            //    query = query.WhereContainsIgnoreCase
            //    (
            //        x => x.Categories.Select(x => x.Name),
            //        category.Value
            //    );
            //}

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
        private static IEnumerable<RestApiOption> GetOptions(Model.Entities.Class row, IRequest request)
        {
            //var editUri = _editFormUri?
            //    .SetParameters(new KeyParameter(row.Key));
            //var cloneUri = _cloneFormUri?
            //    .SetParameters(new KeyParameter(row.Key));
            //var deleteUri = _deleteFormUri?
            //    .SetParameters(new KeyParameter(row.Key));

            //yield return new RestApiOptionHeader(request)
            //{
            //    Text = "webexpress.webapp:header.setting.label"
            //};

            //yield return new RestApiOptionEdit(request)
            //{
            //    PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            //};

            //yield return new RestApiOptionClone(request)
            //{
            //    PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge)
            //};

            //yield return new RestApiOptionCustom(request)
            //{
            //    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._key_.Classes.Index>()?
            //        .SetParameters
            //        (
            //            new KeyParameter(row.Key)
            //        ),
            //    Text = I18N.Translate(request, "kleenestar.core:class.manage.label"),
            //    Icon = new IconBoxesStacked().Class

            //};

            yield return new RestApiOptionSeparator(request);
            //yield return new RestApiOptionDelete(request)
            //{
            //    PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.Small)
            //};
        }

    }
}
