using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// What an object add-on of the editor asks for (<c>Assets/js/editoraddons.js</c>): a filter
    /// over the objects of the installation, read the way the reader is allowed to read them.
    /// </summary>
    /// <param name="Workspace">The key of the workspace, or blank for every workspace.</param>
    /// <param name="Class">The name of the class, or blank for every class. Compared without case.</param>
    /// <param name="Kind">The object kind (<see cref="ObjectKind"/>), or blank for every kind.</param>
    /// <param name="State">"open", "done" or blank for both - read from the workflow's status category.</param>
    /// <param name="Mine">Only the objects assigned to the reader.</param>
    /// <param name="Wql">An additional WQL condition over the object, or blank.</param>
    /// <param name="Max">The number of objects answered (1 to <see cref="EditorObjectQuery.MaxItems"/>).</param>
    public sealed record EditorObjectFilter
    (
        string Workspace = null,
        string Class = null,
        string Kind = null,
        string State = null,
        bool Mine = false,
        string Wql = null,
        int Max = 10
    );

    /// <summary>
    /// One object as an add-on shows it.
    /// </summary>
    /// <param name="Object">The object.</param>
    /// <param name="Category">Its status category, or <see langword="null"/> for a class without a workflow.</param>
    public sealed record EditorObjectItem(Model.Entities.Object Object, StatusCategory Category);

    /// <summary>
    /// The answer to a filter: the objects, how many matched, and whether the scan stopped
    /// before it had looked at everything.
    /// </summary>
    /// <param name="Items">The objects, changed last first, at most the filter's maximum.</param>
    /// <param name="Total">How many objects matched within the scan.</param>
    /// <param name="Truncated">Whether the scan ended at <see cref="EditorObjectQuery.ScanLimit"/>, so <paramref name="Total"/> is a lower bound.</param>
    public sealed record EditorObjectResult(IReadOnlyList<EditorObjectItem> Items, int Total, bool Truncated);

    /// <summary>
    /// Runs the filters of the editor's object add-ons.
    /// </summary>
    /// <remarks>
    /// Workspace, class, kind and assignee are columns and narrow the query; the state and a
    /// WQL condition cannot - the state of an object is a value row of its workflow field, and
    /// a WQL operator such as <c>~</c> does not translate to SQL (the Kanban board filter meets
    /// the same wall) - so they are applied to the rows as they are read, page by page, newest
    /// first, bounded by <see cref="ScanLimit"/>. The count a figure add-on shows is therefore
    /// exact up to that bound and reported as a lower bound beyond it.
    /// <para>
    /// Every read goes through <c>ObjectManager</c>, so what a document shows its reader is
    /// narrowed by the reader's permissions and security levels: the same add-on in the same
    /// document lists different objects for different readers, and never one the reader could
    /// not open.
    /// </para>
    /// </remarks>
    public static class EditorObjectQuery
    {
        /// <summary>
        /// The largest number of objects an add-on may ask for.
        /// </summary>
        public const int MaxItems = 50;

        /// <summary>
        /// The number of objects read at most to answer one filter.
        /// </summary>
        public const int ScanLimit = 2000;

        /// <summary>
        /// The number of objects read per page of the scan.
        /// </summary>
        private const int PageSize = 200;

        /// <summary>
        /// Runs a filter for a reader.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="identityId">The reader, <see cref="Guid.Empty"/> for nobody.</param>
        /// <returns>The result; an empty one for a workspace or class that does not exist.</returns>
        public static EditorObjectResult Run(EditorObjectFilter filter, Guid identityId)
        {
            filter ??= new EditorObjectFilter();

            var max = Math.Clamp(filter.Max, 1, MaxItems);
            var query = Narrow(filter, identityId);

            if (query is null)
            {
                return new EditorObjectResult([], 0, false);
            }

            var state = filter.State?.Trim().ToLowerInvariant();
            var wql = string.IsNullOrWhiteSpace(filter.Wql) ? null : filter.Wql.Trim();
            var categories = CoreHub.StatusManager
                .GetStatusCategories(new Query<StatusCategory>())
                .ToDictionary(x => x.Id);
            var contexts = new Dictionary<Guid, ObjectBoardClassContext>();

            var items = new List<EditorObjectItem>();
            var total = 0;
            var scanned = 0;
            var truncated = false;

            while (true)
            {
                var page = CoreHub.ObjectManager.GetObjects(Narrow(filter, identityId).WithPaging(scanned, PageSize)).ToList();

                scanned += page.Count;

                IEnumerable<Model.Entities.Object> rows = page;

                if (wql is not null)
                {
                    rows = WqlFilter.Apply(wql, rows);
                }

                foreach (var @object in rows)
                {
                    var category = ResolveCategory(@object, contexts, categories);
                    var done = ObjectBoardProjection.Normalize(category?.Name) == "done";

                    if (state == "open" && done || state == "done" && !done)
                    {
                        continue;
                    }

                    total++;

                    if (items.Count < max)
                    {
                        items.Add(new EditorObjectItem(@object, category));
                    }
                }

                if (page.Count < PageSize)
                {
                    break;
                }

                if (scanned >= ScanLimit)
                {
                    truncated = true;

                    break;
                }
            }

            return new EditorObjectResult(items, total, truncated);
        }

        /// <summary>
        /// Reads the documents directly below a document, by summary.
        /// </summary>
        /// <param name="parent">The parent document.</param>
        /// <param name="max">The number of children answered.</param>
        /// <returns>The children the reader may see.</returns>
        public static IReadOnlyList<Model.Entities.Object> Children(Model.Entities.Object parent, int max)
        {
            if (parent is null)
            {
                return [];
            }

            var id = parent.Id;

            return [.. CoreHub.ObjectManager.GetObjects(new Query<Model.Entities.Object>()
                .Where(x => x.ParentId == id)
                .Where(x => x.State == WorkspaceState.Active)
                .OrderByAsc(x => x.Summary)
                .WithPaging(0, Math.Clamp(max, 1, MaxItems)))];
        }

        /// <summary>
        /// Checks a WQL condition before it is run, so an author learns why a list stays empty.
        /// </summary>
        /// <param name="wql">The condition.</param>
        /// <param name="error">The parser's reason when it is refused.</param>
        /// <returns><see langword="true"/> for a blank or valid condition.</returns>
        public static bool TryValidate(string wql, out string error)
        {
            return WqlFilter.TryValidate<Model.Entities.Object>(wql, out error);
        }

        /// <summary>
        /// Builds the query over the columns the filter names, changed last first.
        /// </summary>
        /// <returns>The query, or <see langword="null"/> when a named workspace or class does not exist.</returns>
        private static IQuery<Model.Entities.Object> Narrow(EditorObjectFilter filter, Guid identityId)
        {
            var query = new Query<Model.Entities.Object>()
                .Where(x => x.State == WorkspaceState.Active);

            Guid? workspaceId = null;

            if (!string.IsNullOrWhiteSpace(filter.Workspace))
            {
                var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(filter.Workspace.Trim());

                if (workspace is null)
                {
                    return null;
                }

                var id = workspace.Id;
                workspaceId = id;
                query = query.Where(x => x.WorkspaceId == id);
            }

            if (!string.IsNullOrWhiteSpace(filter.Class))
            {
                var name = filter.Class.Trim();
                var classes = CoreHub.ClassManager
                    .GetClasses(new Query<Model.Entities.Class>())
                    .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                    .Where(x => workspaceId is null || x.WorkspaceId == workspaceId)
                    .Select(x => x.Id)
                    .ToArray();

                if (classes.Length == 0)
                {
                    return null;
                }

                query = query.Where(x => classes.Contains(x.ClassId));
            }

            // not ObjectKind.Normalize: that reads blank as the default kind, and blank here
            // means every kind - as does "any", the value the add-on's choice leads with
            var kind = filter.Kind?.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(kind) && kind != "any")
            {
                query = query.WhereEquals(x => x.Kind, kind);
            }

            if (filter.Mine)
            {
                if (identityId == Guid.Empty)
                {
                    return null;
                }

                query = query.Where(x => x.AssigneeId == identityId);
            }

            return query.OrderByDesc(x => x.Updated);
        }

        /// <summary>
        /// Resolves the status category of an object, building the board context of its class once.
        /// </summary>
        private static StatusCategory ResolveCategory
        (
            Model.Entities.Object @object,
            Dictionary<Guid, ObjectBoardClassContext> contexts,
            IReadOnlyDictionary<Guid, StatusCategory> categories
        )
        {
            if (!contexts.TryGetValue(@object.ClassId, out var context))
            {
                var @class = CoreHub.ClassManager.GetClass(@object.ClassId);
                context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
                contexts[@object.ClassId] = context;
            }

            return ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);
        }
    }
}
