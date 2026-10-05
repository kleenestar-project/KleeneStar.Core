using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// Reads what the reports are computed from: the objects of an insight with the status
    /// categories they went through, replayed from the commit chain of their workflow field,
    /// and the sprints of the workspaces they live in.
    /// </summary>
    /// <remarks>
    /// The history of every workflow field involved is read in one query
    /// (<see cref="ModelHub.GetFieldChanges"/>) rather than per object. A recorded value is
    /// resolved to its category through the statuses of the object's class the way the boards
    /// resolve the current value: by status name, then by status id, then by a category name.
    /// An object with no recorded change of its workflow field (created before the history
    /// existed) is taken to have stood in its current category since its creation.
    /// </remarks>
    public static class InsightReportSource
    {
        /// <summary>
        /// Returns the categories the reports draw, in board order.
        /// </summary>
        /// <returns>The categories.</returns>
        public static IReadOnlyList<InsightReportCategory> Categories()
        {
            return
            [
                .. ObjectBoardProjection.GetOrderedCategories()
                    .Select(x => new InsightReportCategory(
                        ObjectBoardProjection.Normalize(x.Name),
                        ObjectBoardProjection.CategoryLabel(x),
                        x.Color))
            ];
        }

        /// <summary>
        /// Builds the report items of a set of objects.
        /// </summary>
        /// <param name="objects">The objects, as read through the object manager.</param>
        /// <param name="now">The moment the reports are computed at; a change dated after it counts as made at it.</param>
        /// <returns>The items, one per object.</returns>
        public static IReadOnlyList<InsightReportItem> Build(IReadOnlyList<Model.Entities.Object> objects, DateTime now)
        {
            if (objects is null || objects.Count == 0)
            {
                return [];
            }

            var categoriesById = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);

            var contexts = objects
                .Select(x => x.ClassId)
                .Distinct()
                .Select(id => CoreHub.ClassManager.GetClass(id))
                .Where(x => x is not null)
                .ToDictionary(x => x.Id, ObjectBoardProjection.BuildClassContext);

            var fieldIds = contexts.Values
                .Where(x => x.WorkflowField is not null)
                .Select(x => x.WorkflowField.Id)
                .ToHashSet();

            var ids = objects.Select(x => x.Id).ToHashSet();

            var changes = ModelHub.GetFieldChanges(fieldIds)
                .Where(x => ids.Contains(x.ObjectId))
                .GroupBy(x => x.ObjectId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var items = new List<InsightReportItem>(objects.Count);

            foreach (var entity in objects)
            {
                contexts.TryGetValue(entity.ClassId, out var context);

                var states = context?.WorkflowField is null
                    ? []
                    : ReplayStates(entity, context, changes.TryGetValue(entity.Id, out var list) ? list : [], categoriesById, now);

                items.Add(new InsightReportItem
                {
                    Id = entity.Id,
                    Created = entity.Created,
                    States = InsightReportItem.Normalize(entity.Created, states),
                    StoryPoints = entity.StoryPoints,
                    SprintId = entity.SprintId
                });
            }

            return items;
        }

        /// <summary>
        /// Returns the sprints of the workspaces the objects live in, labelled with the workspace
        /// key when there are several.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <returns>The sprints.</returns>
        public static IReadOnlyList<InsightReportSprint> Sprints(IReadOnlyList<Model.Entities.Object> objects)
        {
            var workspaceIds = (objects ?? []).Select(x => x.WorkspaceId).Distinct().ToList();
            var several = workspaceIds.Count > 1;

            return
            [
                .. workspaceIds.SelectMany(workspaceId =>
                {
                    var key = several ? CoreHub.WorkspaceManager.GetWorkspace(workspaceId)?.Key : null;

                    return CoreHub.SprintManager.GetSprintsForWorkspace(workspaceId)
                        .Select(x => new InsightReportSprint(
                            x.Id,
                            string.IsNullOrWhiteSpace(key) ? x.Name : $"{key} · {x.Name}",
                            x.Start,
                            x.End,
                            x.State == SprintState.Completed,
                            x.State == SprintState.Active));
                })
            ];
        }

        /// <summary>
        /// Replays the categories an object went through from the recorded changes of its
        /// workflow field.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <param name="context">The board context of its class.</param>
        /// <param name="changes">The recorded changes of its workflow field, oldest first.</param>
        /// <param name="categoriesById">The status categories.</param>
        /// <param name="now">The moment the reports are computed at.</param>
        /// <returns>The states, oldest first.</returns>
        private static List<InsightReportState> ReplayStates
        (
            Model.Entities.Object entity,
            ObjectBoardClassContext context,
            IReadOnlyList<FieldChange> changes,
            IReadOnlyDictionary<Guid, StatusCategory> categoriesById,
            DateTime now
        )
        {
            var states = new List<InsightReportState>();

            if (changes.Count == 0)
            {
                var current = ObjectBoardProjection.ResolveCategory(entity.Id, context, categoriesById);

                if (current is not null)
                {
                    states.Add(new InsightReportState(entity.Created, ObjectBoardProjection.Normalize(current.Name)));
                }

                return states;
            }

            // a chain that starts after the object was created says, through its first old value,
            // what the object stood in before
            var initial = Resolve(changes[0].OldValue, context, categoriesById);

            if (initial is not null)
            {
                states.Add(new InsightReportState(entity.Created, initial));
            }

            // a change dated in the future (a clock that ran ahead, or seeded history laid out
            // after the moment it was seeded) has happened by the time it is read; left in the
            // future it would count as resolved in a report while the object still reads as open
            foreach (var change in changes)
            {
                var category = Resolve(change.NewValue, context, categoriesById);

                if (category is not null)
                {
                    states.Add(new InsightReportState(change.Changed > now ? now : change.Changed, category));
                }
            }

            return states;
        }

        /// <summary>
        /// Resolves a stored workflow value to the key of its status category.
        /// </summary>
        /// <param name="data">The stored value.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <param name="categoriesById">The status categories.</param>
        /// <returns>The category key, or <see langword="null"/> when the value names none.</returns>
        private static string Resolve(string data, ObjectBoardClassContext context, IReadOnlyDictionary<Guid, StatusCategory> categoriesById)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            var normalized = ObjectBoardProjection.Normalize(data);

            var status = context.Statuses.FirstOrDefault(s => ObjectBoardProjection.Normalize(s.Name) == normalized)
                ?? context.Statuses.FirstOrDefault(s => string.Equals(s.Id.ToString(), data, StringComparison.OrdinalIgnoreCase));

            if (status is not null && categoriesById.TryGetValue(status.CategoryId, out var category))
            {
                return ObjectBoardProjection.Normalize(category.Name);
            }

            // seeded payloads like "done" carry no matching status but name a category
            return categoriesById.Values
                .Where(c => ObjectBoardProjection.Normalize(c.Name) == normalized)
                .Select(c => ObjectBoardProjection.Normalize(c.Name))
                .FirstOrDefault();
        }
    }
}
