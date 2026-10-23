using KleeneStar.Core.WebManager;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebUI.WebControl;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// How one service level target stands on one object, for the compliance report.
    /// </summary>
    /// <param name="ObjectKey">The object.</param>
    /// <param name="Created">When the object was created - the period it is counted in.</param>
    /// <param name="Policy">The agreement.</param>
    /// <param name="Target">The target.</param>
    /// <param name="Status">The state of the target.</param>
    /// <param name="Settled">Whether the target's clock stopped for good.</param>
    /// <param name="Deadline">When the target is due, or <see langword="null"/>.</param>
    public sealed record InsightSlaItem(string ObjectKey, DateTime Created, string Policy, string Target, TypeStatusSla Status, bool Settled, DateTime? Deadline)
    {
        /// <summary>
        /// Gets the outcome the report counts: <c>breached</c>, <c>met</c>, <c>atrisk</c> or <c>running</c>.
        /// </summary>
        public string Outcome => Status switch
        {
            TypeStatusSla.Violated => InsightSlaReport.Breached,
            _ when Settled => InsightSlaReport.Met,
            TypeStatusSla.AtRisk => InsightSlaReport.AtRisk,
            _ => InsightSlaReport.Running
        };
    }

    /// <summary>
    /// Service level compliance: per period, how many targets of the objects created in it were
    /// met, breached, are at risk or still running - and the compliance rate, met of decided.
    /// </summary>
    /// <remarks>
    /// The targets are evaluated now, the way the SLA card and the escalation rules see them, so
    /// a target that is still running counts as running whatever period its object belongs to.
    /// </remarks>
    public static class InsightSlaReport
    {
        /// <summary>The report key.</summary>
        public const string Key = "slacompliance";

        /// <summary>The outcome of a target that was missed.</summary>
        public const string Breached = "breached";

        /// <summary>The outcome of a target that was kept.</summary>
        public const string Met = "met";

        /// <summary>The outcome of a running target past its warning threshold.</summary>
        public const string AtRisk = "atrisk";

        /// <summary>The outcome of a running target.</summary>
        public const string Running = "running";

        /// <summary>
        /// Evaluates the targets of the objects.
        /// </summary>
        /// <param name="objects">The objects, as the reader may see them.</param>
        /// <param name="now">The moment, in UTC.</param>
        /// <returns>One item per object and applying target.</returns>
        public static IReadOnlyList<InsightSlaItem> Collect(IEnumerable<ObjectEntity> objects, DateTime now)
        {
            return [.. (objects ?? []).SelectMany(@object => SlaEvaluations.For(@object, now).Select(x => new InsightSlaItem
            (
                @object.Key,
                @object.Created,
                x.Policy.Name,
                x.Target.Name,
                x.Evaluation.Status,
                x.Evaluation.IsSettled,
                x.Evaluation.Deadline
            )))];
        }

        /// <summary>
        /// Builds the report.
        /// </summary>
        /// <param name="items">The evaluated targets.</param>
        /// <param name="period">The period.</param>
        /// <param name="text">Translates an i18n key.</param>
        /// <param name="culture">The culture of the labels.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult Build(IReadOnlyList<InsightSlaItem> items, InsightReportPeriod period, Func<string, string> text, CultureInfo culture)
        {
            var inPeriod = items.Where(x => x.Created >= period.From && x.Created < period.To).ToList();

            List<double?> Count(string outcome) => [.. period.Buckets.Select(b => (double?)inPeriod.Count(x => b.Contains(x.Created) && x.Outcome == outcome))];

            var met = inPeriod.Count(x => x.Outcome == Met);
            var breached = inPeriod.Count(x => x.Outcome == Breached);
            var atRisk = inPeriod.Count(x => x.Outcome == AtRisk);
            var decided = met + breached;
            var worst = inPeriod
                .Where(x => x.Outcome == Breached)
                .GroupBy(x => $"{x.Policy} · {x.Target}")
                .OrderByDescending(x => x.Count())
                .FirstOrDefault();

            var summary = new List<InsightReportFigure>
            {
                new(text("kleenestar.core:insight.report.sla.compliance"), decided == 0 ? "-" : (100.0 * met / decided).ToString("0.#", culture) + " %"),
                new(text("kleenestar.core:insight.report.series.met"), met.ToString(culture)),
                new(text("kleenestar.core:insight.report.series.breached"), breached.ToString(culture)),
                new(text("kleenestar.core:insight.report.series.atrisk"), atRisk.ToString(culture))
            };

            if (worst is not null)
            {
                summary.Add(new(text("kleenestar.core:insight.report.sla.worst"), $"{worst.Key} ({worst.Count().ToString(culture)})"));
            }

            return new InsightReportResult
            {
                Report = Key,
                Title = text(InsightReports.TitleKey(Key)),
                Description = text(InsightReports.DescriptionKey(Key)),
                Chart = "bar",
                Stacked = true,
                AxisY = text("kleenestar.core:insight.report.axis.targets"),
                Labels = [.. period.Buckets.Select(x => period.Label(x, culture))],
                Series =
                [
                    new InsightReportSeries { Key = Met, Label = text("kleenestar.core:insight.report.series.met"), Type = "bar", Color = "#28A745", Data = Count(Met) },
                    new InsightReportSeries { Key = Breached, Label = text("kleenestar.core:insight.report.series.breached"), Type = "bar", Color = "#DC3545", Data = Count(Breached) },
                    new InsightReportSeries { Key = AtRisk, Label = text("kleenestar.core:insight.report.series.atrisk"), Type = "bar", Color = "#FD7E14", Data = Count(AtRisk) },
                    new InsightReportSeries { Key = Running, Label = text("kleenestar.core:insight.report.series.running"), Type = "bar", Color = "#6C757D", Data = Count(Running) }
                ],
                Summary = summary,
                Empty = inPeriod.Count == 0,
                Notice = items.Count == 0 ? text("kleenestar.core:insight.report.sla.none") : null
            };
        }
    }
}
