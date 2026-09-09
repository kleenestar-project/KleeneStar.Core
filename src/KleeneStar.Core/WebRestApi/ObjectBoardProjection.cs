using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Captures the per-class lookup data needed to project workspace objects onto the
    /// Kanban and Scrum boards — the workflow and priority fields of the class and its
    /// active statuses.
    /// </summary>
    internal sealed class ObjectBoardClassContext
    {
        /// <summary>Gets the class the context belongs to.</summary>
        public Class Class { get; init; }

        /// <summary>Gets the workflow-typed field of the class, or <see langword="null"/>.</summary>
        public Field WorkflowField { get; init; }

        /// <summary>Gets the priority-typed field of the class, or <see langword="null"/>.</summary>
        public Field PriorityField { get; init; }

        /// <summary>
        /// Gets the date-typed field the timeline views read an object's start from, or
        /// <see langword="null"/> when the class models no date at all.
        /// </summary>
        public Field StartDateField { get; init; }

        /// <summary>
        /// Gets the date-typed field the timeline views read an object's end from, or
        /// <see langword="null"/> when the class models only one date.
        /// </summary>
        public Field EndDateField { get; init; }

        /// <summary>Gets the active statuses of the class.</summary>
        public IReadOnlyList<Status> Statuses { get; init; }
    }

    /// <summary>
    /// Provides the shared projection logic of the object board REST endpoints (Kanban,
    /// Scrum backlog, Scrum sprint): resolving an object's workflow-field value to a
    /// status category, its priority value to a display code, and deriving assignee
    /// display data. Mirrors the resolution rules of the portal's issue projection.
    /// </summary>
    internal static class ObjectBoardProjection
    {
        /// <summary>
        /// Builds the board context of a class from the field, status and category
        /// managers.
        /// </summary>
        /// <param name="cls">The class to capture.</param>
        /// <returns>The board projection context of the class.</returns>
        public static ObjectBoardClassContext BuildClassContext(Class cls)
        {
            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(cls.Id))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToList();

            var statuses = CoreHub.StatusManager
                .GetStatuses(new ClassIdParameter(cls.Id))
                .Where(s => s.State == StatusState.Active)
                .ToList();

            var dateFields = ResolveDateFields(fields);

            return new ObjectBoardClassContext
            {
                Class = cls,
                WorkflowField = fields.FirstOrDefault(f => f.FieldType == FieldType.Workflow),
                PriorityField = fields.FirstOrDefault(f => f.FieldType == FieldType.Priority),
                StartDateField = dateFields.Start,
                EndDateField = dateFields.End,
                Statuses = statuses
            };
        }

        /// <summary>
        /// Picks the two date fields the Gantt and the calendar read an object's span from.
        /// </summary>
        /// <remarks>
        /// A class does not declare which of its dates is the start and which the end, so
        /// the fields are matched by name. The tokens are ranked, not merely tested, so a
        /// class carrying both a "PlannedEnd" and a "DueDate" pairs the planned end with the
        /// planned start instead of taking whichever happens to be declared first.
        /// <para>
        /// Only when neither side matched does declaration order decide, first field to
        /// start and second to end. A side that stays unmatched stays null on purpose: an
        /// unrelated date pressed into the empty slot would draw a bar nobody modelled.
        /// </para>
        /// <para>
        /// Fields that merely alias the object's own lifecycle timestamps are skipped — those
        /// are already the fallback span, and letting them win here would make every bar
        /// identical.
        /// </para>
        /// </remarks>
        /// <param name="fields">The active fields of the class.</param>
        /// <returns>The start and end field, either of which may be <see langword="null"/>.</returns>
        private static (Field Start, Field End) ResolveDateFields(IReadOnlyList<Field> fields)
        {
            var dates = fields
                .Where(f => f.FieldType is FieldType.Date or FieldType.Calendar)
                .Where(f => !IsSystemTimestampAlias(f.Name))
                .OrderBy(f => f.Created)
                .ToList();

            if (dates.Count == 0)
            {
                return (null, null);
            }

            var start = BestMatch(dates, StartFieldNames, null);
            var end = BestMatch(dates, EndFieldNames, start);

            if (start is null && end is null && dates.Count > 1)
            {
                return (dates[0], dates[1]);
            }

            return (start, end);
        }

        /// <summary>
        /// Returns the field whose name matches the earliest — that is, the most specific —
        /// token of the supplied list.
        /// </summary>
        /// <param name="dates">The candidate date fields, in declaration order.</param>
        /// <param name="tokens">The tokens, most specific first.</param>
        /// <param name="taken">A field already claimed by the other side, or <see langword="null"/>.</param>
        /// <returns>The best match, or <see langword="null"/> when no name matches.</returns>
        private static Field BestMatch(IReadOnlyList<Field> dates, IReadOnlyList<string> tokens, Field taken)
        {
            foreach (var token in tokens)
            {
                var match = dates.FirstOrDefault(f => f != taken && Normalize(f.Name).Contains(token, StringComparison.Ordinal));

                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the planned span of an object from its class' date fields.
        /// </summary>
        /// <remarks>
        /// A deadline on its own still describes a span: the work has run from the moment the
        /// object was raised until the day it is due, so a stamped end without a stamped start
        /// opens the bar at <see cref="Model.Entities.Object.Created"/> rather than collapsing
        /// it. A start without an end, and an object whose class models no dates at all,
        /// resolve to a milestone on the one day the model actually knows about — which keeps
        /// every object on the timeline instead of dropping the undated ones.
        /// </remarks>
        /// <param name="entity">The object to place.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <returns>The start and end of the object's bar, start never after end.</returns>
        public static (DateTime Start, DateTime End) ResolvePlan(Model.Entities.Object entity, ObjectBoardClassContext context)
        {
            var start = ReadDate(entity.Id, context?.StartDateField);
            var end = ReadDate(entity.Id, context?.EndDateField);

            if (start is null && end is not null)
            {
                start = entity.Created;
            }

            start ??= entity.Created;
            end ??= start;

            return end < start ? (start.Value, start.Value) : (start.Value, end.Value);
        }

        /// <summary>
        /// Reads a date-field value of an object.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="field">The date field, or <see langword="null"/>.</param>
        /// <returns>The stamped date, or <see langword="null"/> when unset or unparsable.</returns>
        public static DateTime? ReadDate(Guid objectId, Field field)
        {
            if (field is null)
            {
                return null;
            }

            var data = CoreHub.ValueManager.GetValue(objectId, field.Id)?.Data;

            return DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Returns the completion percentage a status category reports on the timeline.
        /// </summary>
        /// <param name="category">The resolved category, or <see langword="null"/>.</param>
        /// <returns>The progress in the range 0..100.</returns>
        public static int CategoryProgress(StatusCategory category)
        {
            return Normalize(category?.Name) switch
            {
                "inprogress" => 50,
                "waiting" => 50,
                "done" => 100,
                _ => 0
            };
        }

        /// <summary>
        /// Determines whether a field name aliases one of the object's own lifecycle
        /// timestamps, which the timeline views already have first-hand.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <returns><see langword="true"/> when the name is such an alias.</returns>
        private static bool IsSystemTimestampAlias(string fieldName)
        {
            return Normalize(fieldName) is "created" or "createdat" or "createddate"
                or "updated" or "updatedat" or "updateddate";
        }

        /// <summary>
        /// The name tokens that mark a date field as the start of a span, most specific
        /// first. They are matched against <see cref="Normalize"/>d names, which keep their
        /// umlauts.
        /// </summary>
        private static readonly string[] StartFieldNames = ["start", "beginn", "begin", "from", "von"];

        /// <summary>
        /// The name tokens that mark a date field as the end of a span, most specific first —
        /// a planned end beats a due date, which beats a vague "until".
        /// </summary>
        private static readonly string[] EndFieldNames = ["ende", "end", "due", "deadline", "fällig", "until", "bis", "ziel"];

        /// <summary>
        /// Returns all status categories ordered for board display: To Do, In Progress,
        /// Waiting, Done, then any further categories alphabetically.
        /// </summary>
        /// <returns>The ordered categories.</returns>
        public static IReadOnlyList<StatusCategory> GetOrderedCategories()
        {
            return [.. CoreHub.StatusManager
                .GetStatusCategories(new Query<StatusCategory>())
                .OrderBy(CategoryRank)
                .ThenBy(x => x.Name)];
        }

        /// <summary>
        /// Resolves the status category of an object from its workflow-field value —
        /// first via the stamped status (by normalized name, then by status id), then by
        /// a direct category-name match of the raw payload. Returns
        /// <see langword="null"/> when the object carries no resolvable value.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <param name="categories">All status categories, keyed by id.</param>
        /// <returns>The resolved category, or <see langword="null"/>.</returns>
        public static StatusCategory ResolveCategory(Guid objectId, ObjectBoardClassContext context, IReadOnlyDictionary<Guid, StatusCategory> categories)
        {
            if (context?.WorkflowField is null)
            {
                return null;
            }

            var data = CoreHub.ValueManager.GetValue(objectId, context.WorkflowField.Id)?.Data;
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            var normalized = Normalize(data);

            var status = context.Statuses.FirstOrDefault(s => Normalize(s.Name) == normalized)
                ?? context.Statuses.FirstOrDefault(s => string.Equals(s.Id.ToString(), data, StringComparison.OrdinalIgnoreCase));

            if (status is not null && categories.TryGetValue(status.CategoryId, out var category))
            {
                return category;
            }

            // seeded payloads like "done" carry no matching status but name a category
            return categories.Values.FirstOrDefault(c => Normalize(c.Name) == normalized);
        }

        /// <summary>
        /// Resolves the priority display code of an object from its priority-field
        /// value: a leading <c>P&lt;digit&gt;</c> token wins, well-known severity names
        /// collapse to <c>P1</c>–<c>P4</c>, anything else is shown verbatim. Returns
        /// <see cref="string.Empty"/> when the object carries no priority.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <returns>The priority display code, or an empty string.</returns>
        public static string ResolvePriorityCode(Guid objectId, ObjectBoardClassContext context)
        {
            if (context?.PriorityField is null)
            {
                return string.Empty;
            }

            var data = CoreHub.ValueManager.GetValue(objectId, context.PriorityField.Id)?.Data;
            if (string.IsNullOrWhiteSpace(data))
            {
                return string.Empty;
            }

            var trimmed = data.Trim();

            if (trimmed.Length >= 2
                && (trimmed[0] == 'P' || trimmed[0] == 'p')
                && char.IsDigit(trimmed[1])
                && (trimmed.Length == 2 || !char.IsLetterOrDigit(trimmed[2])))
            {
                return trimmed[..2].ToUpperInvariant();
            }

            return Normalize(trimmed) switch
            {
                "critical" => "P1",
                "high" => "P2",
                "medium" => "P3",
                "low" => "P4",
                _ => trimmed
            };
        }

        /// <summary>
        /// Returns the display label of a status category, splitting camel-cased names
        /// ("InProgress") into words ("In Progress").
        /// </summary>
        /// <param name="category">The category.</param>
        /// <returns>The display label.</returns>
        public static string CategoryLabel(StatusCategory category)
        {
            return Regex.Replace(category?.Name ?? string.Empty, "(?<=[a-z])(?=[A-Z])", " ");
        }

        /// <summary>
        /// Returns the system color CSS class of a status category for the board column
        /// header.
        /// </summary>
        /// <param name="category">The category.</param>
        /// <returns>The CSS class.</returns>
        public static string CategoryColorCss(StatusCategory category)
        {
            return Normalize(category?.Name) switch
            {
                "todo" => "wx-color-secondary",
                "inprogress" => "wx-color-primary",
                "waiting" => "wx-color-warning",
                "done" => "wx-color-success",
                _ => "wx-color-secondary"
            };
        }

        /// <summary>
        /// Returns the collapsed Scrum item status string of a status category as the
        /// scrum controls expect it ("todo", "doing", "waiting", "done").
        /// </summary>
        /// <param name="category">The category, or <see langword="null"/>.</param>
        /// <returns>The item status string.</returns>
        public static string CategoryItemStatus(StatusCategory category)
        {
            return Normalize(category?.Name) switch
            {
                "inprogress" => "doing",
                "waiting" => "waiting",
                "done" => "done",
                _ => "todo"
            };
        }

        /// <summary>
        /// Derives the short initials shown inside an assignee avatar from the display
        /// name (first letter of the first and last word, upper-cased).
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <returns>The initials, or an empty string.</returns>
        public static string Initials(string name)
        {
            var words = (name ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return words.Length switch
            {
                0 => string.Empty,
                1 => char.ToUpperInvariant(words[0][0]).ToString(),
                _ => $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}"
            };
        }

        /// <summary>
        /// Returns a deterministic avatar background color for an identity, so the same
        /// person always renders with the same hue.
        /// </summary>
        /// <param name="identityId">The identity id.</param>
        /// <returns>An HSL CSS color.</returns>
        public static string AvatarColor(Guid identityId)
        {
            var hue = Math.Abs(identityId.GetHashCode()) % 360;

            return $"hsl({hue}, 45%, 45%)";
        }

        /// <summary>
        /// Returns the address of the identity's own picture, or <see langword="null"/> when
        /// it carries none.
        /// </summary>
        /// <remarks>
        /// The initials beside this are the fallback, not the design: a board that answers a
        /// picture gets one, and only a person who has not set one is shown as two letters on
        /// a generated hue. The boards used to send the initials alone, so an identity with a
        /// portrait was still lettered on every card and backlog row while the same person
        /// appeared correctly everywhere else in the application.
        /// </remarks>
        /// <param name="identity">The identity. May be null.</param>
        /// <returns>The picture address, or <see langword="null"/>.</returns>
        public static string AvatarImage(Identity identity)
        {
            return identity?.Avatar?.Uri?.ToString();
        }

        /// <summary>
        /// Normalizes a name for comparison: keeps letters and digits only, lower-cased.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The normalized value.</returns>
        public static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Returns the board display rank of a status category (To Do first, Done last).
        /// </summary>
        /// <param name="category">The category.</param>
        /// <returns>The ordering rank.</returns>
        private static int CategoryRank(StatusCategory category)
        {
            return Normalize(category?.Name) switch
            {
                "todo" => 0,
                "inprogress" => 1,
                "waiting" => 2,
                "done" => 3,
                _ => 4
            };
        }
    }
}
