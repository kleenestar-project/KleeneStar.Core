using System;
using System.Collections.Generic;
using System.Globalization;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// The width of the buckets a report groups time into.
    /// </summary>
    public enum InsightReportInterval
    {
        /// <summary>
        /// One bucket per day.
        /// </summary>
        Day,

        /// <summary>
        /// One bucket per week, starting on Monday.
        /// </summary>
        Week,

        /// <summary>
        /// One bucket per calendar month.
        /// </summary>
        Month
    }

    /// <summary>
    /// One bucket of a report period.
    /// </summary>
    /// <param name="Start">The first moment of the bucket (UTC).</param>
    /// <param name="End">The first moment after the bucket (UTC).</param>
    public sealed record InsightReportBucket(DateTime Start, DateTime End)
    {
        /// <summary>
        /// Returns the moment a state snapshot of the bucket is taken: its last moment, or now
        /// for the bucket that is still running.
        /// </summary>
        /// <param name="now">The current time (UTC).</param>
        /// <returns>The snapshot moment.</returns>
        public DateTime Snapshot(DateTime now)
        {
            var last = End.AddTicks(-1);

            return last > now ? now : last;
        }

        /// <summary>
        /// Determines whether a moment falls into the bucket.
        /// </summary>
        /// <param name="at">The moment.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        public bool Contains(DateTime at)
        {
            return at >= Start && at < End;
        }
    }

    /// <summary>
    /// The time a report covers - the last so many days up to now - cut into buckets of a day,
    /// a week or a month. The first bucket starts at the beginning of the interval the range
    /// starts in, so a weekly report always shows whole weeks; the last bucket is the running one.
    /// </summary>
    public sealed class InsightReportPeriod
    {
        /// <summary>
        /// The most buckets a period is cut into; a wider range is coarsened rather than drawn as
        /// a chart nobody can read.
        /// </summary>
        public const int MaxBuckets = 366;

        /// <summary>
        /// Gets the buckets, oldest first.
        /// </summary>
        public IReadOnlyList<InsightReportBucket> Buckets { get; }

        /// <summary>
        /// Gets the interval the period is cut by.
        /// </summary>
        public InsightReportInterval Interval { get; }

        /// <summary>
        /// Gets the first moment of the period.
        /// </summary>
        public DateTime From => Buckets[0].Start;

        /// <summary>
        /// Gets the first moment after the period.
        /// </summary>
        public DateTime To => Buckets[^1].End;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="buckets">The buckets.</param>
        /// <param name="interval">The interval.</param>
        private InsightReportPeriod(IReadOnlyList<InsightReportBucket> buckets, InsightReportInterval interval)
        {
            Buckets = buckets;
            Interval = interval;
        }

        /// <summary>
        /// Creates the period of the last <paramref name="days"/> days up to <paramref name="now"/>.
        /// </summary>
        /// <param name="now">The current time (UTC).</param>
        /// <param name="days">The number of days covered, at least one.</param>
        /// <param name="interval">The width of a bucket.</param>
        /// <returns>The period.</returns>
        public static InsightReportPeriod Create(DateTime now, int days, InsightReportInterval interval)
        {
            days = Math.Clamp(days, 1, 3660);

            var first = now.Date.AddDays(1 - days);

            // a range too wide for the interval is coarsened, from days to weeks to months
            if (interval == InsightReportInterval.Day && days > MaxBuckets)
            {
                interval = InsightReportInterval.Week;
            }

            if (interval == InsightReportInterval.Week && days / 7 > MaxBuckets)
            {
                interval = InsightReportInterval.Month;
            }

            var buckets = new List<InsightReportBucket>();
            var start = Align(first, interval);

            while (start <= now && buckets.Count < MaxBuckets)
            {
                var end = Advance(start, interval);

                buckets.Add(new InsightReportBucket(start, end));
                start = end;
            }

            if (buckets.Count == 0)
            {
                var aligned = Align(now, interval);

                buckets.Add(new InsightReportBucket(aligned, Advance(aligned, interval)));
            }

            return new InsightReportPeriod(buckets, interval);
        }

        /// <summary>
        /// Parses an interval name, falling back to weeks.
        /// </summary>
        /// <param name="value">The name: day, week or month.</param>
        /// <returns>The interval.</returns>
        public static InsightReportInterval ParseInterval(string value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "day" => InsightReportInterval.Day,
                "month" => InsightReportInterval.Month,
                _ => InsightReportInterval.Week
            };
        }

        /// <summary>
        /// Returns the label of a bucket: its first day, or its month for a monthly period.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="culture">The culture of the reader.</param>
        /// <returns>The label.</returns>
        public string Label(InsightReportBucket bucket, CultureInfo culture)
        {
            culture ??= CultureInfo.InvariantCulture;

            return Interval == InsightReportInterval.Month
                ? bucket.Start.ToString("MMM yyyy", culture)
                : bucket.Start.ToString("d", culture);
        }

        /// <summary>
        /// Returns the start of the interval a moment falls into.
        /// </summary>
        /// <param name="at">The moment.</param>
        /// <param name="interval">The interval.</param>
        /// <returns>The start of the interval.</returns>
        private static DateTime Align(DateTime at, InsightReportInterval interval)
        {
            var day = DateTime.SpecifyKind(at.Date, DateTimeKind.Utc);

            return interval switch
            {
                InsightReportInterval.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
                InsightReportInterval.Month => new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => day
            };
        }

        /// <summary>
        /// Returns the start of the interval after the one starting at a moment.
        /// </summary>
        /// <param name="start">The start of an interval.</param>
        /// <param name="interval">The interval.</param>
        /// <returns>The start of the next interval.</returns>
        private static DateTime Advance(DateTime start, InsightReportInterval interval)
        {
            return interval switch
            {
                InsightReportInterval.Week => start.AddDays(7),
                InsightReportInterval.Month => start.AddMonths(1),
                _ => start.AddDays(1)
            };
        }
    }
}
