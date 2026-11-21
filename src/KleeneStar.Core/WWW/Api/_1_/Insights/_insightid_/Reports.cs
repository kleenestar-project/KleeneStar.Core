using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebInsight.Reports;
using KleeneStar.Core.WebReport;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The data behind an insight's reports tab: one report, computed from the history of the
    /// objects the insight selects.
    /// </summary>
    /// <remarks>
    /// <c>GET /api/1/insights/{insightid}/reports</c> answers the catalog of reports (key, title,
    /// description) when no report is named, and the report itself for
    /// <c>?report=cumulativeflow|velocity|averageage|createdresolved|resolutiontime</c>, over the
    /// last <c>range</c> days (default 90) cut by <c>interval</c> (<c>day</c>, <c>week</c> - the
    /// default - or <c>month</c>); the velocity chart takes the last <c>sprints</c> sprints
    /// (default 8) instead. The objects are read through the object manager, so a report shows
    /// only what its reader may see; a caller who may not read the insight gets a 403.
    /// <para>
    /// <c>slacompliance</c> evaluates the service level targets of the objects created in the
    /// period. <c>&amp;format=csv</c> answers any report as a CSV file - the series per period,
    /// for <c>slacompliance</c> one row per object and target - with <c>&amp;delimiter=semicolon</c>
    /// for spreadsheets that expect it.
    /// </para>
    /// </remarks>
    [Cache]
    public sealed class Reports : IRestApi
    {
        /// <summary>
        /// The serializer options of the answer.
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Answers the report catalog or one report.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The JSON response.</returns>
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

            var report = request?.GetParameter("report")?.Value?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(report))
            {
                return Json(InsightReports.Keys.Select(key => new
                {
                    key,
                    title = I18N.Translate(request, InsightReports.TitleKey(key)),
                    description = I18N.Translate(request, InsightReports.DescriptionKey(key))
                }));
            }

            if (!InsightReports.Keys.Contains(report))
            {
                return new ResponseBadRequest(new StatusMessage($"unknown report '{report}'."));
            }

            var now = DateTime.UtcNow;
            var culture = Culture(request);
            string Text(string key) => I18N.Translate(request, key);

            var objects = InsightScope.GetObjects(insight);
            var items = InsightReportSource.Build(objects, now);
            var period = InsightReportPeriod.Create
            (
                now,
                ParseInt(request, "range", 90, 7, 3650),
                InsightReportPeriod.ParseInterval(request?.GetParameter("interval")?.Value)
            );

            var csv = string.Equals(request?.GetParameter("format")?.Value, "csv", StringComparison.OrdinalIgnoreCase);

            if (report == InsightSlaReport.Key)
            {
                var targets = InsightSlaReport.Collect(objects, now);

                return csv
                    ? SlaCsv(targets, period, request, insight.Name)
                    : Json(InsightSlaReport.Build(targets, period, Text, culture));
            }

            var result = report switch
            {
                InsightReports.CumulativeFlow => InsightReports.BuildCumulativeFlow(items, period, now, InsightReportSource.Categories(), Text, culture),
                InsightReports.Velocity => InsightReports.BuildVelocity(items, InsightReportSource.Sprints(objects), now, ParseInt(request, "sprints", 8, 1, 50), Text, culture),
                InsightReports.AverageAge => InsightReports.BuildAverageAge(items, period, now, Text, culture),
                InsightReports.CreatedVsResolved => InsightReports.BuildCreatedVsResolved(items, period, now, Text, culture),
                _ => InsightReports.BuildResolutionTime(items, period, now, Text, culture)
            };

            // objects whose class has no workflow cannot flow or be resolved; say so rather than
            // let a report look smaller than the table beside it without a reason
            var untracked = items.Count(x => !x.HasStates);

            if (untracked > 0 && result.Notice is null && report != InsightReports.Velocity)
            {
                result.Notice = string.Format(culture, Text("kleenestar.core:insight.report.untracked"), untracked);
            }

            return csv ? Csv(result, request, insight.Name) : Json(result);
        }

        /// <summary>
        /// Writes a chart report as a table: one row per period, one column per series, followed
        /// by the summary figures.
        /// </summary>
        /// <param name="result">The report.</param>
        /// <param name="request">The request.</param>
        /// <param name="insight">The insight's name, for the file name.</param>
        /// <returns>The CSV download.</returns>
        private static IResponse Csv(InsightReportResult result, IRequest request, string insight)
        {
            var document = new CsvDocument(CsvDocument.DelimiterOf(request));

            document.Row([string.Empty, .. result.Series.Select(x => x.Label)]);

            for (var index = 0; index < result.Labels.Count; index++)
            {
                document.Row([result.Labels[index], .. result.Series.Select(x => index < x.Data.Count ? CsvDocument.Number(x.Data[index]) : string.Empty)]);
            }

            if (result.Summary.Count > 0)
            {
                document.Row();

                foreach (var figure in result.Summary)
                {
                    document.Row(figure.Label, figure.Value);
                }
            }

            return document.ToResponse($"{insight}-{result.Report}.csv");
        }

        /// <summary>
        /// Writes the service level targets of the period, one row per object and target.
        /// </summary>
        /// <param name="targets">The evaluated targets.</param>
        /// <param name="period">The period.</param>
        /// <param name="request">The request.</param>
        /// <param name="insight">The insight's name, for the file name.</param>
        /// <returns>The CSV download.</returns>
        private static IResponse SlaCsv(IReadOnlyList<InsightSlaItem> targets, InsightReportPeriod period, IRequest request, string insight)
        {
            var document = new CsvDocument(CsvDocument.DelimiterOf(request));

            document.Row("object", "created", "agreement", "target", "outcome", "deadline");

            foreach (var item in targets.Where(x => x.Created >= period.From && x.Created < period.To).OrderBy(x => x.Created))
            {
                document.Row(item.ObjectKey, CsvDocument.Date(item.Created), item.Policy, item.Target, item.Outcome, CsvDocument.Date(item.Deadline?.ToUniversalTime()));
            }

            return document.ToResponse($"{insight}-{InsightSlaReport.Key}.csv");
        }

        /// <summary>
        /// Serializes an answer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The JSON response.</returns>
        private static IResponse Json(object value)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, _options))
            }
            .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Returns the culture of the request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The culture, or the invariant one.</returns>
        private static CultureInfo Culture(IRequest request)
        {
            try
            {
                return request?.Culture ?? CultureInfo.InvariantCulture;
            }
            catch
            {
                return CultureInfo.InvariantCulture;
            }
        }

        /// <summary>
        /// Parses an integer parameter within bounds.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="name">The parameter name.</param>
        /// <param name="fallback">The value when the parameter is missing or no number.</param>
        /// <param name="min">The smallest value.</param>
        /// <param name="max">The largest value.</param>
        /// <returns>The value.</returns>
        private static int ParseInt(IRequest request, string name, int fallback, int min, int max)
        {
            return int.TryParse(request?.GetParameter(name)?.Value, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;
        }
    }
}
