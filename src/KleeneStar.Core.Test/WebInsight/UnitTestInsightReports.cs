using KleeneStar.Core.WebInsight.Reports;
using System.Globalization;

namespace KleeneStar.Core.Test.WebInsight
{
    /// <summary>
    /// Tests the reports of an insight on hand-built histories, whose answers can be counted on
    /// one's fingers: the replay of states, the period buckets, and the five reports.
    /// </summary>
    public class UnitTestInsightReports
    {
        /// <summary>
        /// "Now" of every test: a Wednesday, so a weekly period has a running week.
        /// </summary>
        private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// The categories in board order.
        /// </summary>
        private static readonly IReadOnlyList<InsightReportCategory> Categories =
        [
            new("todo", "To Do", "#FF5733"),
            new("inprogress", "In Progress", "#33C1FF"),
            new("waiting", "Waiting", "#FFC300"),
            new("done", "Done", "#28A745")
        ];

        /// <summary>
        /// Returns the i18n key itself, so a test reads which text a report asks for.
        /// </summary>
        private static string Text(string key) => key;

        /// <summary>
        /// Builds an item from a creation day and (day, category) steps, all relative to now.
        /// </summary>
        private static InsightReportItem Item(int createdDaysAgo, params (int DaysAgo, string Category)[] steps)
        {
            var created = Now.AddDays(-createdDaysAgo);

            return new InsightReportItem
            {
                Id = Guid.NewGuid(),
                Created = created,
                States = InsightReportItem.Normalize(created, steps.Select(x => new InsightReportState(Now.AddDays(-x.DaysAgo), x.Category)))
            };
        }

        /// <summary>
        /// The states replay: before creation there is none, then the last state entered.
        /// </summary>
        [Fact]
        public void Item_CategoryAt_ReplaysTheStates()
        {
            var item = Item(10, (10, "todo"), (6, "inprogress"), (2, "done"));

            Assert.Null(item.CategoryAt(Now.AddDays(-11)));
            Assert.Equal("todo", item.CategoryAt(Now.AddDays(-8)));
            Assert.Equal("inprogress", item.CategoryAt(Now.AddDays(-3)));
            Assert.Equal("done", item.CategoryAt(Now));
            Assert.True(item.IsOpenAt(Now.AddDays(-3)));
            Assert.False(item.IsOpenAt(Now));
        }

        /// <summary>
        /// A repeated state is dropped, a first state is pulled to the creation, and a state
        /// recorded at the same moment as the one before it replaces it.
        /// </summary>
        [Fact]
        public void Item_Normalize_CollapsesTheHistory()
        {
            var created = Now.AddDays(-5);

            var states = InsightReportItem.Normalize(created,
            [
                new InsightReportState(created.AddMinutes(1), "todo"),
                new InsightReportState(created.AddDays(1), "todo"),
                new InsightReportState(created.AddDays(2), "inprogress"),
                new InsightReportState(created.AddDays(2), "waiting"),
                new InsightReportState(created.AddDays(3), "done")
            ]);

            Assert.Equal(["todo", "waiting", "done"], states.Select(x => x.Category));
            Assert.Equal(created, states[0].At);
        }

        /// <summary>
        /// A reopened object is resolved twice; one created done is resolved at its creation.
        /// </summary>
        [Fact]
        public void Item_Resolutions_CountsEveryEntryIntoDone()
        {
            var reopened = Item(10, (10, "todo"), (8, "done"), (5, "inprogress"), (1, "done"));
            var bornDone = Item(4, (4, "done"));

            Assert.Equal([Now.AddDays(-8), Now.AddDays(-1)], reopened.Resolutions());
            Assert.Equal([Now.AddDays(-4)], bornDone.Resolutions());
        }

        /// <summary>
        /// A weekly period starts on the Monday of the week the range starts in and ends with the
        /// running week; the snapshot of the running bucket is now.
        /// </summary>
        [Fact]
        public void Period_Week_CoversWholeWeeks()
        {
            var period = InsightReportPeriod.Create(Now, 14, InsightReportInterval.Week);

            Assert.Equal(DayOfWeek.Monday, period.From.DayOfWeek);
            Assert.True(period.From <= Now.Date.AddDays(-13));
            Assert.True(period.Buckets[^1].Contains(Now));
            Assert.Equal(Now, period.Buckets[^1].Snapshot(Now));
            Assert.All(period.Buckets, x => Assert.Equal(7, (x.End - x.Start).Days));
        }

        /// <summary>
        /// A range too wide for days is coarsened to weeks rather than drawn as hundreds of bars.
        /// </summary>
        [Fact]
        public void Period_TooManyDays_IsCoarsened()
        {
            var period = InsightReportPeriod.Create(Now, 1000, InsightReportInterval.Day);

            Assert.Equal(InsightReportInterval.Week, period.Interval);
            Assert.True(period.Buckets.Count <= InsightReportPeriod.MaxBuckets);
        }

        /// <summary>
        /// The cumulative flow counts, at the end of every bucket, the objects in each category -
        /// and leaves out an object without states.
        /// </summary>
        [Fact]
        public void CumulativeFlow_CountsTheStatesAtEachBucketEnd()
        {
            var items = new[]
            {
                Item(2, (2, "todo"), (1, "inprogress")),
                Item(2, (2, "done")),
                new InsightReportItem { Id = Guid.NewGuid(), Created = Now.AddDays(-2) }
            };

            var period = InsightReportPeriod.Create(Now, 4, InsightReportInterval.Day);
            var report = InsightReports.BuildCumulativeFlow(items, period, Now, Categories, Text, CultureInfo.InvariantCulture);

            double? Last(string key) => report.Series.Single(x => x.Key == key).Data[^1];

            Assert.True(report.Stacked);
            Assert.Equal(4, report.Labels.Count);
            Assert.Equal(1, Last("inprogress"));
            Assert.Equal(1, Last("done"));
            Assert.Equal(0, Last("todo"));
            Assert.Equal("done", report.Series[0].Key);

            // four days ago nothing existed yet
            Assert.All(report.Series, x => Assert.Equal(0, x.Data[0]));
        }

        /// <summary>
        /// Velocity: story points where estimated; committed is what the sprint holds, completed
        /// what was done at its end; only running and completed sprints are shown.
        /// </summary>
        [Fact]
        public void Velocity_CommitsAndCompletesStoryPoints()
        {
            var sprint = new InsightReportSprint(Guid.NewGuid(), "Sprint 1", Now.AddDays(-20), Now.AddDays(-6), Completed: true, Active: false);
            var planned = new InsightReportSprint(Guid.NewGuid(), "Sprint 3", Now.AddDays(2), Now.AddDays(16), Completed: false, Active: false);

            InsightReportItem Planned(int points, params (int, string)[] steps)
            {
                var item = Item(25, steps);

                return new InsightReportItem { Id = item.Id, Created = item.Created, States = item.States, StoryPoints = points, SprintId = sprint.Id };
            }

            var items = new[]
            {
                Planned(5, (25, "todo"), (10, "done")),
                Planned(3, (25, "todo"), (2, "done")),
                Planned(2, (25, "todo"))
            };

            var report = InsightReports.BuildVelocity(items, [sprint, planned], Now, 8, Text, CultureInfo.InvariantCulture);

            Assert.Equal(["Sprint 1"], report.Labels);
            Assert.Equal(10, report.Series.Single(x => x.Key == "committed").Data[0]);
            Assert.Equal(5, report.Series.Single(x => x.Key == "completed").Data[0]);
            Assert.Equal("kleenestar.core:insight.report.axis.points", report.AxisY);
        }

        /// <summary>
        /// Velocity counts objects when nothing in the sprints is estimated.
        /// </summary>
        [Fact]
        public void Velocity_WithoutEstimates_CountsObjects()
        {
            var sprint = new InsightReportSprint(Guid.NewGuid(), "Sprint 2", Now.AddDays(-3), Now.AddDays(11), Completed: false, Active: true);
            var item = Item(5, (5, "todo"), (1, "done"));

            var report = InsightReports.BuildVelocity(
                [new InsightReportItem { Id = item.Id, Created = item.Created, States = item.States, SprintId = sprint.Id }],
                [sprint], Now, 8, Text, CultureInfo.InvariantCulture);

            Assert.Equal(1, report.Series.Single(x => x.Key == "completed").Data[0]);
            Assert.Equal("kleenestar.core:insight.report.axis.objects", report.AxisY);
        }

        /// <summary>
        /// The average age is taken over the objects open at the end of the bucket.
        /// </summary>
        [Fact]
        public void AverageAge_AveragesTheOpenObjects()
        {
            var items = new[]
            {
                Item(4, (4, "todo")),
                Item(2, (2, "inprogress")),
                Item(6, (6, "todo"), (5, "done"))
            };

            var period = InsightReportPeriod.Create(Now, 1, InsightReportInterval.Day);
            var report = InsightReports.BuildAverageAge(items, period, Now, Text, CultureInfo.InvariantCulture);

            Assert.Equal(3, report.Series.Single(x => x.Key == "age").Data[0]);
            Assert.Equal(2, report.Series.Single(x => x.Key == "open").Data[0]);
        }

        /// <summary>
        /// Created and resolved are counted in the bucket they happened in; open is the snapshot.
        /// </summary>
        [Fact]
        public void CreatedVsResolved_CountsPerBucket()
        {
            var items = new[]
            {
                Item(1, (1, "todo")),
                Item(1, (1, "todo"), (0, "done")),
                Item(10, (10, "todo"), (0, "done"))
            };

            var period = InsightReportPeriod.Create(Now, 2, InsightReportInterval.Day);
            var report = InsightReports.BuildCreatedVsResolved(items, period, Now, Text, CultureInfo.InvariantCulture);

            Assert.Equal([2, 0], report.Series.Single(x => x.Key == "created").Data);
            Assert.Equal([0, 2], report.Series.Single(x => x.Key == "resolved").Data);
            Assert.Equal([3, 1], report.Series.Single(x => x.Key == "open").Data);
        }

        /// <summary>
        /// The resolution time is the time from creation to resolution, averaged per bucket of
        /// resolution; the median is a figure of its own.
        /// </summary>
        [Fact]
        public void ResolutionTime_AveragesTheResolvedObjects()
        {
            var items = new[]
            {
                Item(4, (4, "todo"), (0, "done")),
                Item(10, (10, "todo"), (0, "done")),
                Item(3, (3, "todo"))
            };

            var period = InsightReportPeriod.Create(Now, 1, InsightReportInterval.Day);
            var report = InsightReports.BuildResolutionTime(items, period, Now, Text, CultureInfo.InvariantCulture);

            Assert.Equal(7, report.Series.Single(x => x.Key == "time").Data[0]);
            Assert.Equal(2, report.Series.Single(x => x.Key == "resolved").Data[0]);
            Assert.False(report.Empty);
            Assert.Equal(7, InsightReports.Median([4, 10]));
        }

        /// <summary>
        /// A report over objects without any history says it is empty instead of drawing zeros.
        /// </summary>
        [Fact]
        public void Reports_WithoutStates_AreEmpty()
        {
            var items = new[] { new InsightReportItem { Id = Guid.NewGuid(), Created = Now.AddDays(-1) } };
            var period = InsightReportPeriod.Create(Now, 7, InsightReportInterval.Day);

            Assert.True(InsightReports.BuildCumulativeFlow(items, period, Now, Categories, Text, CultureInfo.InvariantCulture).Empty);
            Assert.True(InsightReports.BuildAverageAge(items, period, Now, Text, CultureInfo.InvariantCulture).Empty);
            Assert.True(InsightReports.BuildCreatedVsResolved(items, period, Now, Text, CultureInfo.InvariantCulture).Empty);
            Assert.True(InsightReports.BuildResolutionTime(items, period, Now, Text, CultureInfo.InvariantCulture).Empty);
        }
    }
}
