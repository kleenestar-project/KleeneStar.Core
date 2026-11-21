using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_
{
    /// <summary>
    /// Provides a selection of classes belonging to the specified workspace.
    /// </summary>
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : RestApiSelection<Model.Entities.Class>
    {
        /// <summary>
        /// Creates the query context used to retrieve classes.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Applies a name search to the class query.
        /// </summary>
        protected override IQuery<Model.Entities.Class> Filter(string filter, IQuery<Model.Entities.Class> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Retrieves selectable classes from the workspace addressed by the route.
        /// </summary>
        protected override IEnumerable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Class> query, IQueryContext context, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

            if (workspace is null)
            {
                return [];
            }

            query = query.WhereEquals(x => x.WorkspaceId, workspace.Id);

            return CoreHub.ClassManager.GetClasses(query, context)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = x.Name
                });
        }
    }
}
