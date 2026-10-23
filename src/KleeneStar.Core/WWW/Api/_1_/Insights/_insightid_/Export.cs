using KleeneStar.Core.WebAutomation.Actions;
using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebReport;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The objects of an insight as a CSV file: <c>GET /api/1/insights/{insightid}/export</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>columns</c> chooses and orders the columns, comma-separated: <c>key</c>, <c>summary</c>,
    /// <c>kind</c>, <c>workspace</c>, <c>class</c>, <c>state</c>, <c>assignee</c>, <c>creator</c>,
    /// <c>created</c>, <c>updated</c>, <c>sla</c> (the worst service level outcome) and
    /// <c>field:&lt;name&gt;</c> for a field of the object's class (empty where the class has no
    /// such field). <c>from</c>/<c>to</c> (ISO dates, <c>to</c> exclusive) narrow to a period of
    /// <c>date=created</c> (default) or <c>updated</c>; <c>delimiter=semicolon</c> separates with
    /// semicolons.
    /// </para>
    /// <para>
    /// The objects are those the insight shows its reader - across workspaces, narrowed by
    /// grants and clearance like every list - so an export never holds more than the screen.
    /// </para>
    /// </remarks>
    [Cache]
    public sealed class Export : IRestApi
    {
        /// <summary>
        /// The columns of an export that names none.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultColumns = ["key", "summary", "workspace", "class", "state", "assignee", "created", "updated"];

        /// <summary>
        /// The most rows one export writes; two years of a busy service desk.
        /// </summary>
        public const int MaxRows = 200_000;

        /// <summary>
        /// Answers the export.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The CSV download, <c>403</c> or <c>404</c>.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Get(IRequest request)
        {
            var insight = InsightScope.ResolveReadable(request);

            if (insight is null)
            {
                return InsightScope.Resolve(request) is null
                    ? new ResponseNotFound(new StatusMessage("insight not found."))
                    : new ResponseForbidden();
            }

            var columns = ParseColumns(request?.GetParameter("columns")?.Value);
            var byUpdate = string.Equals(request?.GetParameter("date")?.Value, "updated", StringComparison.OrdinalIgnoreCase);
            var from = ParseDate(request?.GetParameter("from")?.Value);
            var to = ParseDate(request?.GetParameter("to")?.Value);

            var objects = InsightScope.GetObjects(insight)
                .Where(x => from is null || (byUpdate ? x.Updated : x.Created) >= from)
                .Where(x => to is null || (byUpdate ? x.Updated : x.Created) < to)
                .OrderBy(x => x.Created)
                .Take(MaxRows)
                .ToList();

            return Write(objects, columns, CsvDocument.DelimiterOf(request), DateTime.UtcNow).ToResponse($"{insight.Name}.csv");
        }

        /// <summary>
        /// Writes the objects.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <param name="columns">The columns.</param>
        /// <param name="delimiter">The separator.</param>
        /// <param name="now">The moment the service levels are evaluated at.</param>
        /// <returns>The document.</returns>
        public static CsvDocument Write(IReadOnlyList<ObjectEntity> objects, IReadOnlyList<string> columns, char delimiter, DateTime now)
        {
            var document = new CsvDocument(delimiter);
            var workspaces = new Dictionary<Guid, string>();
            var classes = new Dictionary<Guid, string>();
            var identities = new Dictionary<Guid, string>();
            var fields = new Dictionary<Guid, List<Model.Entities.Field>>();

            string Cached(Dictionary<Guid, string> cache, Guid? id, Func<Guid, string> read)
            {
                if (id is not Guid key || key == Guid.Empty)
                {
                    return string.Empty;
                }

                if (!cache.TryGetValue(key, out var value))
                {
                    value = read(key) ?? string.Empty;
                    cache[key] = value;
                }

                return value;
            }

            string FieldValue(ObjectEntity @object, string name)
            {
                if (!fields.TryGetValue(@object.ClassId, out var classFields))
                {
                    classFields = StandardActions.FieldsOf(@object.ClassId);
                    fields[@object.ClassId] = classFields;
                }

                var field = classFields.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

                return field is null ? string.Empty : CoreHub.ValueManager.GetValue(@object.Id, field.Id)?.Data ?? string.Empty;
            }

            string Cell(ObjectEntity @object, string column) => column switch
            {
                "key" => @object.Key,
                "summary" => @object.Summary,
                "kind" => @object.Kind,
                "workspace" => Cached(workspaces, @object.WorkspaceId, id => CoreHub.WorkspaceManager.GetWorkspace(id)?.Key),
                "class" => Cached(classes, @object.ClassId, id => CoreHub.ClassManager.GetClass(id)?.Name),
                "state" => SlaClock.ResolveStatus(@object, CoreHub.FieldManager, CoreHub.ValueManager, CoreHub.WorkflowManager)?.Name ?? string.Empty,
                "assignee" => Cached(identities, @object.AssigneeId, id => CoreHub.IdentityManager.GetIdentity(id)?.Name),
                "creator" => Cached(identities, @object.CreatorId, id => CoreHub.IdentityManager.GetIdentity(id)?.Name),
                "created" => CsvDocument.Date(@object.Created),
                "updated" => CsvDocument.Date(@object.Updated),
                "sla" => WorstSla(@object, now),
                _ when column.StartsWith("field:", StringComparison.Ordinal) => FieldValue(@object, column["field:".Length..]),
                _ => string.Empty
            };

            document.Row(columns);

            foreach (var @object in objects)
            {
                document.Row(columns.Select(column => Cell(@object, column)));
            }

            return document;
        }

        /// <summary>
        /// Reads the columns a request asks for.
        /// </summary>
        /// <param name="value">The comma-separated columns.</param>
        /// <returns>The known columns in the order asked, or the defaults.</returns>
        public static IReadOnlyList<string> ParseColumns(string value)
        {
            var known = new HashSet<string>(["key", "summary", "kind", "workspace", "class", "state", "assignee", "creator", "created", "updated", "sla"]);
            var columns = (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.StartsWith("field:", StringComparison.OrdinalIgnoreCase) ? "field:" + x["field:".Length..].Trim() : x.ToLowerInvariant())
                .Where(x => known.Contains(x) || (x.StartsWith("field:", StringComparison.Ordinal) && x.Length > "field:".Length))
                .Distinct()
                .ToList();

            return columns.Count == 0 ? DefaultColumns : columns;
        }

        /// <summary>
        /// Reads a date.
        /// </summary>
        /// <param name="value">An ISO date or date and time.</param>
        /// <returns>The moment in UTC, or <see langword="null"/>.</returns>
        private static DateTime? ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date
                : null;
        }

        /// <summary>
        /// Returns the worst service level outcome of an object.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="now">The moment.</param>
        /// <returns><c>breached</c>, <c>atrisk</c>, <c>running</c>, <c>met</c>, or empty without targets.</returns>
        private static string WorstSla(ObjectEntity @object, DateTime now)
        {
            var outcomes = WebInsight.Reports.InsightSlaReport.Collect([@object], now).Select(x => x.Outcome).ToList();

            foreach (var outcome in new[] { WebInsight.Reports.InsightSlaReport.Breached, WebInsight.Reports.InsightSlaReport.AtRisk, WebInsight.Reports.InsightSlaReport.Running, WebInsight.Reports.InsightSlaReport.Met })
            {
                if (outcomes.Contains(outcome))
                {
                    return outcome;
                }
            }

            return string.Empty;
        }
    }
}
