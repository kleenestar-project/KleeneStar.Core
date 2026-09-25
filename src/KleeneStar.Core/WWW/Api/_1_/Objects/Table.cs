using KleeneStar.Core.WebFragment.Search;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
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
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// REST table that lists objects across <em>every</em> workspace — the result set of the
    /// global "search over all workspaces" page. Unlike the workspace-scoped object table,
    /// this endpoint applies no workspace filter and surfaces the owning workspace as a column.
    /// </summary>
    /// <remarks>
    /// While the page runs a saved search, the table's address carries its id
    /// (<see cref="SavedSearchRun.Parameter"/>): the columns then come from the saved search and a
    /// change of them is stored there, and the quickfilters defined for it narrow the rows. A reader
    /// who may not change the saved search keeps a layout of their own for it, so moving a column
    /// works for them too without rearranging it for everybody.
    /// </remarks>
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

            // the cells follow the columns in the order they are shown - the caller's or the
            // saved search's layout - since the client pairs them by position
            var order = columns?.Select(c => c.Id).ToList() ?? [];

            return CoreHub.ObjectManager.GetObjects(query, context)
                .Select(x =>
                {
                    var cells = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["key"] = x.Key,
                        ["summary"] = x.Summary,
                        ["workspace"] = workspaceNames.TryGetValue(x.WorkspaceId, out var name) ? name : string.Empty,
                        ["description"] = ProseText.ToPlainText(x.Description)
                    };

                    return new
                    {
                        Object = x,
                        Cells = order.Select(id => new RestApiTableCell() { Content = cells.GetValueOrDefault(id) }).ToList()
                    };
                })
                .Select(x => new RestApiTableRow
                {
                    Id = x.Object.Id.ToString(),
                    Cells = x.Cells,
                    // the options column is where the table offers the column chooser, and it
                    // appears only for rows that carry a menu - without one the results could
                    // not be rearranged at all
                    Options = Options(x.Object, request).Select(o => o.ToJson()),
                    Uri = GetUri(x.Object)?.ToString(),
                    Image = x.Object.Icon?.Uri?.ToString()
                });
        }

        /// <summary>
        /// Returns how many objects the search found in total, before paging narrows it, so the
        /// pager offers every page - without it the table reported the size of the page it
        /// returned ("50 of 50") and the rest of the result was unreachable.
        /// </summary>
        /// <param name="query">The filtered query, without paging applied.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The number of objects in the whole result.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.CountObjects(query);
        }

        /// <summary>
        /// Builds the menu of a result row.
        /// </summary>
        /// <param name="object">The object of the row.</param>
        /// <param name="request">The request, for the culture of the labels.</param>
        /// <returns>The row options.</returns>
        private static IEnumerable<RestApiOption> Options(Model.Entities.Object @object, IRequest request)
        {
            yield return new RestApiOptionCustom(request)
            {
                // a payload string is not a control: nothing on the client resolves a key in
                // it, so the label is translated here like the built-in options translate theirs
                Text = I18N.Translate(request, "kleenestar.core:search.results.open.label"),
                Icon = new IconArrowUpRightFromSquare(),
                Uri = GetUri(@object)
            };
        }

        /// <summary>
        /// Retrieves the columns: while a saved search runs, in the layout it carries (a reader who
        /// may not change it sees their own layout for it, once they made one); otherwise in the
        /// caller's own layout of the table.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The effective column collection.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveColums(IRequest request)
        {
            var running = SavedSearchRun.Resolve(request);

            if (running is null)
            {
                return base.RetrieveColums(request);
            }

            return TableLayout.Apply(Layout(running, request), RetrieveDefaultColumns(request));
        }

        /// <summary>
        /// Stores a changed column layout: into the saved search that runs when the caller may
        /// change it, into the caller's own layout for that saved search when not, and into the
        /// caller's own layout of the table otherwise.
        /// </summary>
        /// <param name="columns">The reordered columns as resolved by the framework.</param>
        /// <param name="request">The triggering request.</param>
        protected override void UpdateColumns(IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            var running = SavedSearchRun.Resolve(request);

            if (running is null)
            {
                base.UpdateColumns(columns, request);
            }
            else if (SavedSearchAuthorization.MayUpdate(running, request))
            {
                CoreHub.SavedSearchManager.SetColumns(running.Id, TableLayout.Serialize(columns));
            }
            else
            {
                CoreHub.SessionManager.SetTableLayout(request, ReaderLayoutKey(running), columns);
            }
        }

        /// <summary>
        /// Returns the layout the results are shown in for the request - the one a new saved search
        /// made from the page takes over, so it opens the way the search looked when it was saved.
        /// </summary>
        /// <param name="request">The request naming the caller and, maybe, the saved search that runs.</param>
        /// <returns>The layout as stored (<see cref="TableLayout"/>), or null for the default one.</returns>
        public static string CurrentLayout(IRequest request)
        {
            var running = SavedSearchRun.Resolve(request);

            return TableLayout.Serialize(running is null
                ? CoreHub.SessionManager.GetTableLayout(request, typeof(Table).FullName)
                : Layout(running, request));
        }

        /// <summary>
        /// Returns the layout a saved search is shown in for the caller: their own for it when they
        /// may not change it and made one, else the saved search's, else their own layout of the
        /// table - a saved search from before layouts were kept shows what the caller is used to.
        /// </summary>
        /// <param name="running">The saved search that runs.</param>
        /// <param name="request">The request naming the caller.</param>
        /// <returns>The layout entries, or null for the default one.</returns>
        private static IReadOnlyList<RestApiTableColumnUpdate> Layout(Model.Entities.SavedSearch running, IRequest request)
        {
            if (!SavedSearchAuthorization.MayUpdate(running, request)
                && CoreHub.SessionManager.GetTableLayout(request, ReaderLayoutKey(running)) is { Count: > 0 } own)
            {
                return own;
            }

            return TableLayout.Parse(running.Columns)
                ?? CoreHub.SessionManager.GetTableLayout(request, typeof(Table).FullName);
        }

        /// <summary>
        /// Returns the key under which a reader who may not change a saved search keeps their own
        /// layout of it.
        /// </summary>
        /// <param name="savedSearch">The saved search.</param>
        /// <returns>The layout key.</returns>
        private static string ReaderLayoutKey(Model.Entities.SavedSearch savedSearch)
        {
            return $"{typeof(Table).FullName}@savedsearch:{savedSearch.Id}";
        }

        /// <summary>
        /// Applies the active quickfilters: the ones defined for the saved search that runs. A
        /// filter of another view is skipped by the shared support even when its id is sent.
        /// </summary>
        /// <param name="filters">The active quickfilter ids.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<Model.Entities.Object> Filter(IEnumerable<string> filters, IQuery<Model.Entities.Object> query, IRequest request)
        {
            return CustomQuickfilterSupport.Apply(filters, query, global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter.ViewKey);
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
