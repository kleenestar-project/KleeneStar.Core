using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The workflow statuses a Kanban board of one kind in one workspace offers: what its
    /// columns hold, what a card may be dropped into and what the column dialog lists.
    /// </summary>
    /// <remarks>
    /// Statuses are defined per class and a board spans every class of its kind in the
    /// workspace, so the board addresses a status by its <b>name</b> across classes - the
    /// normalized name (<see cref="Key(Status)"/>) is the id the framework's board carries. An
    /// <c>Open</c> of the incident class and an <c>Open</c> of the problem class are one entry;
    /// a drop resolves it back to the status of the dropped card's own class. That mirrors how
    /// the default board already merges classes into one column per status category.
    /// <para>
    /// Only the statuses a class's workflow takes part in are listed - a status the state
    /// machine never reaches is nothing a card could be moved into. A class without a workflow
    /// field contributes nothing, and its cards cannot change columns.
    /// </para>
    /// </remarks>
    public sealed class KanbanStatusCatalog
    {
        private readonly List<Entry> _entries = [];
        private readonly Dictionary<Guid, (Field Field, Workflow Workflow)> _workflows = [];

        /// <summary>
        /// Gets whether the board offers no status at all - no class of the kind has a
        /// workflow. The board then behaves as it did before statuses existed.
        /// </summary>
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>
        /// Gets the statuses in the order the board lists them: by status category (to do, in
        /// progress, waiting, done), then in the order the workflows declare them. Each chip is
        /// coloured like its category, the colour the object page shows the same state in.
        /// </summary>
        public IReadOnlyList<RestApiKanbanStatus> Statuses => [.. _entries.Select(x => new RestApiKanbanStatus
        {
            Id = x.Key,
            Label = x.Label,
            Color = string.IsNullOrWhiteSpace(x.Color) ? null : new PropertyColorBackgroundBadge(x.Color)
        })];

        /// <summary>
        /// Builds the catalog of the classes of a kind in a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace of the board.</param>
        /// <param name="kind">The object kind the board lists.</param>
        /// <returns>The catalog.</returns>
        public static KanbanStatusCatalog Build(Guid workspaceId, string kind)
        {
            var catalog = new KanbanStatusCatalog();
            var categories = ObjectBoardProjection.GetOrderedCategories();
            var rank = categories
                .Select((category, index) => (category.Id, index))
                .ToDictionary(x => x.Id, x => x.index);
            var colors = categories.ToDictionary(x => x.Id, x => x.Color);

            var classes = CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .Where(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name);

            var order = 0;

            foreach (var cls in classes)
            {
                var field = ObjectBoardProjection.BuildClassContext(cls).WorkflowField;
                var workflow = field?.WorkflowId is Guid workflowId
                    ? CoreHub.WorkflowManager.GetWorkflowWithStructure(workflowId)
                    : null;

                if (workflow is null)
                {
                    continue;
                }

                catalog._workflows[cls.Id] = (field, workflow);

                foreach (var status in (workflow.Statuses ?? []).Where(x => x.State == StatusState.Active))
                {
                    var key = Key(status);

                    if (key.Length == 0)
                    {
                        continue;
                    }

                    var entry = catalog._entries.FirstOrDefault(x => x.Key == key);

                    if (entry is null)
                    {
                        // a name shared across classes keeps the colour of its first category,
                        // like it keeps the spelling of its first class
                        entry = new Entry(key, status.Name, colors.GetValueOrDefault(status.CategoryId), order++);
                        catalog._entries.Add(entry);
                    }

                    entry.CategoryIds.Add(status.CategoryId);
                    entry.Rank = Math.Min(entry.Rank, rank.GetValueOrDefault(status.CategoryId, int.MaxValue));
                }
            }

            catalog._entries.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Order.CompareTo(b.Order));

            return catalog;
        }

        /// <summary>
        /// Answers whether the board offers a status.
        /// </summary>
        /// <param name="key">The status key.</param>
        /// <returns>True when a class of the board has a status of that name.</returns>
        public bool Contains(string key)
        {
            return _entries.Any(x => x.Key == key);
        }

        /// <summary>
        /// Returns the keys of the statuses that belong to a status category in at least one
        /// class, in catalog order.
        /// </summary>
        /// <param name="categoryId">The category, or null for none.</param>
        /// <returns>The keys; empty for a column without a category.</returns>
        public IReadOnlyList<string> KeysOfCategory(Guid? categoryId)
        {
            return categoryId is Guid id
                ? [.. _entries.Where(x => x.CategoryIds.Contains(id)).Select(x => x.Key)]
                : [];
        }

        /// <summary>
        /// Returns the workflow field and workflow a class moves its objects through.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The pair, or null values for a class without a workflow.</returns>
        public (Field Field, Workflow Workflow) WorkflowOf(Guid classId)
        {
            return _workflows.TryGetValue(classId, out var pair) ? pair : (null, null);
        }

        /// <summary>
        /// Returns the key the board addresses a status by: its name reduced to lower-case
        /// letters and digits, the same normalization the board already matches a stored
        /// workflow value with.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>The key, or an empty string.</returns>
        public static string Key(Status status)
        {
            return ObjectBoardProjection.Normalize(status?.Name);
        }

        /// <summary>
        /// Reads the statuses stored on a board column.
        /// </summary>
        /// <param name="stored">The value of <see cref="KanbanBoardColumn.Statuses"/>.</param>
        /// <returns>
        /// Null when the column follows its category; otherwise the keys, possibly none.
        /// </returns>
        public static IReadOnlyList<string> Parse(string stored)
        {
            return stored is null
                ? null
                : [.. stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
        }

        /// <summary>
        /// Writes status keys the way <see cref="KanbanBoardColumn.Statuses"/> stores them.
        /// </summary>
        /// <param name="keys">The keys.</param>
        /// <returns>The stored value; an empty string for an explicitly empty column.</returns>
        public static string Format(IEnumerable<string> keys)
        {
            return string.Join(',', (keys ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        }

        /// <summary>
        /// One status of the board, merged across the classes that share its name.
        /// </summary>
        /// <param name="key">The normalized name.</param>
        /// <param name="label">The name shown, the one the first class spells it with.</param>
        /// <param name="color">The colour of the status category it first appeared in, or null.</param>
        /// <param name="order">The position of its first appearance.</param>
        private sealed class Entry(string key, string label, string color, int order)
        {
            public string Key { get; } = key;

            public string Label { get; } = label;

            public string Color { get; } = color;

            public int Order { get; } = order;

            public int Rank { get; set; } = int.MaxValue;

            public HashSet<Guid> CategoryIds { get; } = [];
        }
    }
}
