using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// Computes the reports of an insight's reports tab from the history of its objects:
    /// cumulative flow, velocity, average age, created vs. resolved and resolution time.
    /// </summary>
    /// <remarks>
    /// Every report reads the same model (<see cref="InsightReportItem"/>): when an object was
    /// created and which status categories it went through. "Resolved" means entering the done
    /// category; reopening and resolving again counts again. An object whose class has no
    /// workflow has no state and is left out of every report, because it can neither flow nor be
    /// resolved. The texts come through <c>text</c>, which resolves an i18n key, so this class
    /// stays free of requests and stores.
    /// </remarks>
    public static class InsightReports
    {
        /// <summary>The key of the cumulative flow diagram.</summary>
        public const string CumulativeFlow = "cumulativeflow";

        /// <summary>The key of the velocity chart.</summary>
        public const string Velocity = "velocity";

        /// <summary>The key of the average age report.</summary>
        public const string AverageAge = "averageage";

        /// <summary>The key of the created vs. resolved report.</summary>
        public const string CreatedVsResolved = "createdresolved";

        /// <summary>The key of the resolution time report.</summary>
        public const string ResolutionTime = "resolutiontime";

        /// <summary>
        /// Gets the reports in the order the picker offers them.
        /// </summary>
        public static IReadOnlyList<string> Keys { get; } =
        [
            CumulativeFlow, Velocity, AverageAge, CreatedVsResolved, ResolutionTime
        ];

        /// <summary>
        /// Returns the i18n key of a report's name.
        /// </summary>
        /// <param name="report">The report key.</param>
        /// <returns>The i18n key.</returns>
        public static string TitleKey(string report) => $"kleenestar.core:insight.report.{report}.title";

        /// <summary>
        /// Returns the i18n key of the sentence that explains a report.
        /// </summary>
        /// <param name="report">The report key.</param>
        /// <returns>The i18n key.</returns>
        public static string DescriptionKey(string report) => $"kleenestar.core:insight.report.{report}.description";

        /// <summary>
        /// Builds the cumulative flow diagram: per bucket, how many objects stood in each status
        /// category at its end - a stacked area whose bands widen where work piles up.
        /// </summary>
        /// <param name="items">The objects.</param>
        /// <param name="period">The period.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="categories">The categories in board order.</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult BuildCumulativeFlow
        (
            IReadOnlyList<InsightReportItem> items,
            InsightReportPeriod period,
            DateTime now,
            IReadOnlyList<InsightReportCategory> categories,
            Func<string, string> text,
            CultureInfo culture
        )
        {
            var tracked = items.Where(x => x.HasStates).ToList();
            var snapshots = period.Buckets.Select(x => x.Snapshot(now)).ToList();

            // categories nobody declared (a seeded payload naming one directly) are still drawn,
            // behind the declared ones, so no object drops out of the stack
            var known = categories.Select(x => x.Key).ToHashSet();
            var extra = tracked
                .SelectMany(x => x.States)
                .Select(x => x.Category)
                .Where(x => !known.Contains(x))
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new InsightReportCategory(x, x, null));

            // the done band is drawn at the bottom of the stack, so the work still to do sits on top
            var order = categories.Concat(extra)
                .OrderBy(x => x.Key == InsightReportItem.Done ? 0 : 1)
                .ToList();

            var series = order
                .Select(category => new InsightReportSeries
                {
                    Key = category.Key,
                    Label = category.Label,
                    Color = category.Color,
                    Fill = true,
                    Data = [.. snapshots.Select(at => (double?)tracked.Count(x => x.CategoryAt(at) == category.Key))]
                })
                .ToList();

            var open = tracked.Count(x => x.IsOpenAt(now));

            return new InsightReportResult
            {
                Report = CumulativeFlow,
                Title = text(TitleKey(CumulativeFlow)),
                Description = text(DescriptionKey(CumulativeFlow)),
                Chart = "line",
                Stacked = true,
                AxisY = text("kleenestar.core:insight.report.axis.objects"),
                Labels = [.. period.Buckets.Select(x => period.Label(x, culture))],
                Series = series,
                Empty = tracked.Count == 0,
                Summary =
                [
                    new(text("kleenestar.core:insight.report.figure.open"), Format(open, culture)),
                    new(text("kleenestar.core:insight.report.figure.done"), Format(tracked.Count(x => x.CategoryAt(now) == InsightReportItem.Done), culture)),
                    new(text("kleenestar.core:insight.report.figure.total"), Format(tracked.Count, culture))
                ]
            };
        }

        /// <summary>
        /// Builds the velocity chart: per sprint, the work committed to it and the work completed
        /// in it, in story points - or in objects, when nothing in the sprints is estimated.
        /// </summary>
        /// <remarks>
        /// The commitment is what is planned into the sprint now: the store keeps no history of
        /// sprint membership. An object counts as completed when it stood in the done category
        /// at the end of the sprint (now, for the running one).
        /// </remarks>
        /// <param name="items">The objects.</param>
        /// <param name="sprints">The sprints of the workspaces the objects live in.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="limit">The most recent sprints shown.</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult BuildVelocity
        (
            IReadOnlyList<InsightReportItem> items,
            IReadOnlyList<InsightReportSprint> sprints,
            DateTime now,
            int limit,
            Func<string, string> text,
            CultureInfo culture
        )
        {
            var shown = sprints
                .Where(x => x.Completed || x.Active)
                .OrderBy(x => x.End ?? x.Start ?? DateTime.MaxValue)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .TakeLast(Math.Max(1, limit))
                .ToList();

            var bySprint = items
                .Where(x => x.SprintId is not null)
                .GroupBy(x => x.SprintId.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            var planned = shown
                .SelectMany(x => bySprint.TryGetValue(x.Id, out var list) ? list : [])
                .ToList();

            // story points where the team estimates, objects where it does not
            var points = planned.Any(x => x.StoryPoints is > 0);
            double Weight(InsightReportItem item) => points ? item.StoryPoints ?? 0 : 1;

            var committed = new List<double?>();
            var completed = new List<double?>();

            foreach (var sprint in shown)
            {
                var list = bySprint.TryGetValue(sprint.Id, out var found) ? found : [];
                var end = sprint.Active || sprint.End is null || sprint.End > now ? now : sprint.End.Value;

                committed.Add(list.Sum(Weight));
                completed.Add(list.Where(x => x.CategoryAt(end) == InsightReportItem.Done).Sum(Weight));
            }

            var finished = shown
                .Select((sprint, index) => (sprint, index))
                .Where(x => x.sprint.Completed)
                .Select(x => completed[x.index] ?? 0)
                .ToList();

            var unit = points
                ? text("kleenestar.core:insight.report.axis.points")
                : text("kleenestar.core:insight.report.axis.objects");

            return new InsightReportResult
            {
                Report = Velocity,
                Title = text(TitleKey(Velocity)),
                Description = text(DescriptionKey(Velocity)),
                Chart = "bar",
                AxisY = unit,
                Labels = [.. shown.Select(x => x.Label)],
                Series =
                [
                    new InsightReportSeries
                    {
                        Key = "committed",
                        Label = text("kleenestar.core:insight.report.series.committed"),
                        Type = "bar",
                        Color = "#9AA5B1",
                        Data = committed
                    },
                    new InsightReportSeries
                    {
                        Key = "completed",
                        Label = text("kleenestar.core:insight.report.series.completed"),
                        Type = "bar",
                        Color = "#28A745",
                        Data = completed
                    }
                ],
                Empty = shown.Count == 0,
                Notice = shown.Count == 0 ? text("kleenestar.core:insight.report.velocity.nosprint") : null,
                Summary =
                [
                    new(text("kleenestar.core:insight.report.figure.velocity"), finished.Count == 0 ? "-" : Format(finished.Average(), culture)),
                    new(text("kleenestar.core:insight.report.figure.sprints"), Format(finished.Count, culture)),
                    new(text("kleenestar.core:insight.report.figure.unit"), unit)
                ]
            };
        }

        /// <summary>
        /// Builds the average age report: per bucket, how long the objects open at its end had
        /// been open on average, beside how many there were.
        /// </summary>
        /// <param name="items">The objects.</param>
        /// <param name="period">The period.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult BuildAverageAge
        (
            IReadOnlyList<InsightReportItem> items,
            InsightReportPeriod period,
            DateTime now,
            Func<string, string> text,
            CultureInfo culture
        )
        {
            var tracked = items.Where(x => x.HasStates).ToList();
            var ages = new List<double?>();
            var counts = new List<double?>();

            foreach (var at in period.Buckets.Select(x => x.Snapshot(now)))
            {
                var open = tracked.Where(x => x.IsOpenAt(at)).ToList();

                counts.Add(open.Count);
                ages.Add(open.Count == 0 ? null : open.Average(x => (at - x.Created).TotalDays));
            }

            var (factor, axis) = TimeScale(ages);

            var current = tracked.Where(x => x.IsOpenAt(now)).ToList();
            var oldest = current.Count == 0 ? (double?)null : current.Max(x => (now - x.Created).TotalDays);

            return new InsightReportResult
            {
                Report = AverageAge,
                Title = text(TitleKey(AverageAge)),
                Description = text(DescriptionKey(AverageAge)),
                Chart = "bar",
                AxisY = text(axis),
                AxisY1 = text("kleenestar.core:insight.report.axis.objects"),
                Labels = [.. period.Buckets.Select(x => period.Label(x, culture))],
                Series =
                [
                    new InsightReportSeries
                    {
                        Key = "age",
                        Label = text("kleenestar.core:insight.report.series.age"),
                        Type = "bar",
                        Color = "#33C1FF",
                        Data = Scale(ages, factor)
                    },
                    new InsightReportSeries
                    {
                        Key = "open",
                        Label = text("kleenestar.core:insight.report.series.open"),
                        Type = "line",
                        Color = "#FF5733",
                        Axis = "y1",
                        Data = counts
                    }
                ],
                Empty = tracked.Count == 0,
                Summary =
                [
                    new(text("kleenestar.core:insight.report.figure.averageage"), current.Count == 0 ? "-" : FormatDuration(current.Average(x => (now - x.Created).TotalDays), text, culture)),
                    new(text("kleenestar.core:insight.report.figure.oldest"), oldest is null ? "-" : FormatDuration(oldest.Value, text, culture)),
                    new(text("kleenestar.core:insight.report.figure.open"), Format(current.Count, culture))
                ]
            };
        }

        /// <summary>
        /// Builds the created vs. resolved report: per bucket, how many objects were created and
        /// how many resolved, beside how many were open at its end - a line that climbs while
        /// more arrives than is finished.
        /// </summary>
        /// <param name="items">The objects.</param>
        /// <param name="period">The period.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult BuildCreatedVsResolved
        (
            IReadOnlyList<InsightReportItem> items,
            InsightReportPeriod period,
            DateTime now,
            Func<string, string> text,
            CultureInfo culture
        )
        {
            var tracked = items.Where(x => x.HasStates).ToList();
            var resolutions = tracked.SelectMany(x => x.Resolutions()).ToList();

            var created = period.Buckets.Select(b => (double?)tracked.Count(x => b.Contains(x.Created))).ToList();
            var resolved = period.Buckets.Select(b => (double?)resolutions.Count(b.Contains)).ToList();
            var open = period.Buckets.Select(b => (double?)tracked.Count(x => x.IsOpenAt(b.Snapshot(now)))).ToList();

            var createdTotal = created.Sum(x => x ?? 0);
            var resolvedTotal = resolved.Sum(x => x ?? 0);

            return new InsightReportResult
            {
                Report = CreatedVsResolved,
                Title = text(TitleKey(CreatedVsResolved)),
                Description = text(DescriptionKey(CreatedVsResolved)),
                Chart = "bar",
                AxisY = text("kleenestar.core:insight.report.axis.objects"),
                AxisY1 = text("kleenestar.core:insight.report.axis.open"),
                Labels = [.. period.Buckets.Select(x => period.Label(x, culture))],
                Series =
                [
                    new InsightReportSeries
                    {
                        Key = "created",
                        Label = text("kleenestar.core:insight.report.series.created"),
                        Type = "bar",
                        Color = "#FF5733",
                        Data = created
                    },
                    new InsightReportSeries
                    {
                        Key = "resolved",
                        Label = text("kleenestar.core:insight.report.series.resolved"),
                        Type = "bar",
                        Color = "#28A745",
                        Data = resolved
                    },
                    new InsightReportSeries
                    {
                        Key = "open",
                        Label = text("kleenestar.core:insight.report.series.open"),
                        Type = "line",
                        Color = "#6F42C1",
                        Axis = "y1",
                        Data = open
                    }
                ],
                Empty = tracked.Count == 0,
                Summary =
                [
                    new(text("kleenestar.core:insight.report.figure.created"), Format(createdTotal, culture)),
                    new(text("kleenestar.core:insight.report.figure.resolved"), Format(resolvedTotal, culture)),
                    new(text("kleenestar.core:insight.report.figure.balance"), Format(createdTotal - resolvedTotal, culture, signed: true))
                ]
            };
        }

        /// <summary>
        /// Builds the resolution time report: per bucket, how long the objects resolved in it
        /// had taken from their creation on average, beside how many there were.
        /// </summary>
        /// <param name="items">The objects.</param>
        /// <param name="period">The period.</param>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The report.</returns>
        public static InsightReportResult BuildResolutionTime
        (
            IReadOnlyList<InsightReportItem> items,
            InsightReportPeriod period,
            DateTime now,
            Func<string, string> text,
            CultureInfo culture
        )
        {
            var events = items
                .Where(x => x.HasStates)
                .SelectMany(x => x.Resolutions().Select(at => (At: at, Days: (at - x.Created).TotalDays)))
                .Where(x => x.At >= period.From && x.At < period.To)
                .ToList();

            var averages = new List<double?>();
            var counts = new List<double?>();

            foreach (var bucket in period.Buckets)
            {
                var inBucket = events.Where(x => bucket.Contains(x.At)).ToList();

                counts.Add(inBucket.Count);
                averages.Add(inBucket.Count == 0 ? null : inBucket.Average(x => x.Days));
            }

            var (factor, axis) = TimeScale(averages);

            var days = events.Select(x => x.Days).OrderBy(x => x).ToList();

            return new InsightReportResult
            {
                Report = ResolutionTime,
                Title = text(TitleKey(ResolutionTime)),
                Description = text(DescriptionKey(ResolutionTime)),
                Chart = "bar",
                AxisY = text(axis),
                AxisY1 = text("kleenestar.core:insight.report.axis.objects"),
                Labels = [.. period.Buckets.Select(x => period.Label(x, culture))],
                Series =
                [
                    new InsightReportSeries
                    {
                        Key = "time",
                        Label = text("kleenestar.core:insight.report.series.time"),
                        Type = "bar",
                        Color = "#28A745",
                        Data = Scale(averages, factor)
                    },
                    new InsightReportSeries
                    {
                        Key = "resolved",
                        Label = text("kleenestar.core:insight.report.series.resolved"),
                        Type = "line",
                        Color = "#6F42C1",
                        Axis = "y1",
                        Data = counts
                    }
                ],
                Empty = events.Count == 0,
                Summary =
                [
                    new(text("kleenestar.core:insight.report.figure.averagetime"), days.Count == 0 ? "-" : FormatDuration(days.Average(), text, culture)),
                    new(text("kleenestar.core:insight.report.figure.mediantime"), days.Count == 0 ? "-" : FormatDuration(Median(days), text, culture)),
                    new(text("kleenestar.core:insight.report.figure.resolved"), Format(days.Count, culture))
                ]
            };
        }

        /// <summary>
        /// Returns the median of a sorted list.
        /// </summary>
        /// <param name="sorted">The values, sorted ascending; not empty.</param>
        /// <returns>The median.</returns>
        public static double Median(IReadOnlyList<double> sorted)
        {
            var middle = sorted.Count / 2;

            return sorted.Count % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        /// <summary>
        /// Rounds a figure to one decimal.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The rounded value.</returns>
        private static double Round(double value)
        {
            return Math.Round(value, 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Formats a figure with at most one decimal.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="culture">The culture.</param>
        /// <param name="signed">Whether a positive value carries its sign.</param>
        /// <returns>The text.</returns>
        private static string Format(double value, CultureInfo culture, bool signed = false)
        {
            var text = Round(value).ToString("0.#", culture ?? CultureInfo.InvariantCulture);

            return signed && value > 0 ? "+" + text : text;
        }

        /// <summary>
        /// Formats a duration: in days, or in hours when it is shorter than a day - a resolution
        /// within the hour is not "0 days".
        /// </summary>
        /// <param name="days">The duration in days.</param>
        /// <param name="text">Resolves an i18n key.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The text.</returns>
        private static string FormatDuration(double days, Func<string, string> text, CultureInfo culture)
        {
            return days < 1
                ? $"{Format(days * 24, culture)} {text("kleenestar.core:insight.report.unit.hours")}"
                : $"{Format(days, culture)} {text("kleenestar.core:insight.report.unit.days")}";
        }

        /// <summary>
        /// Picks the unit a series of durations is drawn in: hours when every value stays below
        /// a day, days otherwise.
        /// </summary>
        /// <param name="days">The values in days.</param>
        /// <returns>The factor from days to the unit, and the i18n key of the axis title.</returns>
        private static (double Factor, string Axis) TimeScale(IEnumerable<double?> days)
        {
            var values = days.Where(x => x is not null).Select(x => x.Value).ToList();

            return values.Count > 0 && values.Max() < 1
                ? (24, "kleenestar.core:insight.report.axis.hours")
                : (1, "kleenestar.core:insight.report.axis.days");
        }

        /// <summary>
        /// Converts a series of durations from days into a unit and rounds it.
        /// </summary>
        /// <param name="days">The values in days.</param>
        /// <param name="factor">The factor from days to the unit.</param>
        /// <returns>The converted values.</returns>
        private static List<double?> Scale(IEnumerable<double?> days, double factor)
        {
            return [.. days.Select(x => x is null ? (double?)null : Round(x.Value * factor))];
        }
    }
}
