using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebControl;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// REST table that lists objects across <em>every</em> workspace — the result set of the
    /// global "search over all workspaces" page. Unlike the workspace-scoped object table,
    /// this endpoint applies no workspace filter and surfaces the owning workspace as a column.
    /// </summary>
    [Title("kleenestar.core:search.results.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
        }

        /// <summary>
        /// Creates a new query context backed by the application database.
        /// </summary>
        /// <returns>An <see cref="IQueryContext"/> instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the default column definitions of the global results table.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The default columns.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "key",
                Label = I18N.Translate(request, "kleenestar.core:search.results.column.key"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "summary",
                Label = I18N.Translate(request, "kleenestar.core:search.results.column.summary"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "workspace",
                Label = I18N.Translate(request, "kleenestar.core:search.results.column.workspace"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:search.results.column.description"),
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves the matching object rows across all workspaces.
        /// </summary>
        /// <param name="query">The query that defines the criteria for selecting rows.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="columns">The columns to include in the result.</param>
        /// <param name="request">The request object.</param>
        /// <returns>The matching rows.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<Model.Entities.Object> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            // names only - the rows themselves are narrowed by the object manager
            var workspaceNames = CoreHub.WorkspaceManager
                .GetWorkspaces(new Query<Workspace>())
                .GroupBy(w => w.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            return CoreHub.ObjectManager.GetObjects(query, context)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() { Content = x.Key },
                        new() { Content = x.Summary },
                        new() { Content = workspaceNames.TryGetValue(x.WorkspaceId, out var name) ? name : string.Empty },
                        new() { Content = ProseText.ToPlainText(x.Description) }
                    ],
                    Uri = GetUri(x)?.ToString(),
                    Image = x.Icon?.Uri?.ToString()
                });
        }

        /// <summary>
        /// Applies the substring (summary) search filter.
        /// </summary>
        /// <param name="filter">The filter expression.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(string filter, IQuery<Model.Entities.Object> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Summary, filter);
        }

        /// <summary>
        /// Resolves the detail-page URI of the given object.
        /// </summary>
        /// <param name="row">The object.</param>
        /// <returns>The object detail URI.</returns>
        private static IUri GetUri(Model.Entities.Object row)
        {
            return global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                .ResolveDetailUri(row);
        }
    }
}
