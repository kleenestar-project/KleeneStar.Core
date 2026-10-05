using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.InsightManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestInsightManager
    {
        /// <summary>
        /// Initializes the in-memory database and CoreHub for a single test case.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the insight and that <c>GetInsight</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetInsight_RoundTrip()
        {
            Seed(nameof(Add_Then_GetInsight_RoundTrip));

            var insight = Sample("Overview");
            CoreHub.InsightManager.Add(insight);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Overview", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var insight = Sample("Initial");
            CoreHub.InsightManager.Add(insight);

            insight.Name = "Renamed";
            CoreHub.InsightManager.Update(insight);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the insight and raises the
        /// <see cref="KleeneStar.Core.WebManager.IInsightManager.InsightRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var insight = Sample("DeleteMe");
            CoreHub.InsightManager.Add(insight);

            Insight raised = null;
            CoreHub.InsightManager.InsightRemoved += (_, d) => raised = d;

            CoreHub.InsightManager.Remove(insight.Id);

            Assert.Null(CoreHub.InsightManager.GetInsight(insight.Id));
            Assert.NotNull(raised);
            Assert.Equal(insight.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetInsights(IQuery)</c> returns insights from the database.
        /// </summary>
        [Fact]
        public void GetInsights_ReturnsAllStored()
        {
            Seed(nameof(GetInsights_ReturnsAllStored));

            CoreHub.InsightManager.Add(Sample("Alpha"));
            CoreHub.InsightManager.Add(Sample("Beta"));

            var result = CoreHub.InsightManager.GetInsights(new Query<Insight>()).ToList();

            Assert.True(result.Count >= 2);
            Assert.Contains(result, d => d.Name == "Alpha");
            Assert.Contains(result, d => d.Name == "Beta");
        }

        /// <summary>
        /// Verifies that a column-only update renames, resizes, recolors and reorders columns and
        /// that the changes survive a reload, while the widgets of the surviving columns are kept.
        /// </summary>
        [Fact]
        public void SetColumns_RenameReorderRecolor_SurvivesReloadAndKeepsWidgets()
        {
            Seed(nameof(SetColumns_RenameReorderRecolor_SurvivesReloadAndKeepsWidgets));

            var insight = SampleWithBoard("Board");
            CoreHub.InsightManager.Add(insight);

            var first = insight.Columns[0];
            var second = insight.Columns[1];

            // reorder (second first), rename + resize + recolor the first, keep the second as-is
            CoreHub.InsightManager.SetColumns(insight.Id,
            [
                new DashboardColumn(second.Id) { Name = second.Name, Size = second.Size, Color = second.Color },
                new DashboardColumn(first.Id) { Name = "Renamed", Size = "50%", Color = "#123456" }
            ]);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);
            var ordered = loaded.Columns.OrderBy(c => c.Position).ToList();

            Assert.Equal(2, ordered.Count);
            Assert.Equal(second.Id, ordered[0].Id);
            Assert.Equal(first.Id, ordered[1].Id);
            Assert.Equal("Renamed", ordered[1].Name);
            Assert.Equal("50%", ordered[1].Size);
            Assert.Equal("#123456", ordered[1].Color);

            // the widgets of the surviving columns are preserved by a column-only update
            Assert.Equal(2, ordered[0].Widgets.Count + ordered[1].Widgets.Count);
        }

        /// <summary>
        /// Verifies that adding a column (an entry with an empty id) persists a new empty column and
        /// leaves the existing columns and their widgets intact.
        /// </summary>
        [Fact]
        public void SetColumns_Add_PersistsNewColumn()
        {
            Seed(nameof(SetColumns_Add_PersistsNewColumn));

            var insight = SampleWithBoard("Board");
            CoreHub.InsightManager.Add(insight);

            var first = insight.Columns[0];
            var second = insight.Columns[1];

            CoreHub.InsightManager.SetColumns(insight.Id,
            [
                new DashboardColumn(first.Id) { Name = first.Name, Size = "1fr" },
                new DashboardColumn(second.Id) { Name = second.Name, Size = "1fr" },
                new DashboardColumn(Guid.Empty) { Name = "New column", Size = "1fr" }
            ]);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);

            Assert.Equal(3, loaded.Columns.Count);
            Assert.Contains(loaded.Columns, c => c.Name == "New column" && c.Widgets.Count == 0);
        }

        /// <summary>
        /// Verifies that deleting a column (omitting it from the desired set) removes it together with
        /// its widgets.
        /// </summary>
        [Fact]
        public void SetColumns_Delete_RemovesColumnAndWidgets()
        {
            Seed(nameof(SetColumns_Delete_RemovesColumnAndWidgets));

            var insight = SampleWithBoard("Board");
            CoreHub.InsightManager.Add(insight);

            var first = insight.Columns[0];

            CoreHub.InsightManager.SetColumns(insight.Id,
            [
                new DashboardColumn(first.Id) { Name = first.Name, Size = first.Size }
            ]);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);

            Assert.Single(loaded.Columns);
            Assert.Equal(first.Id, loaded.Columns[0].Id);
        }

        /// <summary>
        /// Verifies that a full board update rebuilds a column's widgets and that the per-widget type,
        /// name, color and params survive a reload.
        /// </summary>
        [Fact]
        public void SetBoard_WidgetSettings_SurviveReload()
        {
            Seed(nameof(SetBoard_WidgetSettings_SurviveReload));

            var insight = SampleWithBoard("Board");
            CoreHub.InsightManager.Add(insight);

            var first = insight.Columns[0];

            CoreHub.InsightManager.SetBoard(insight.Id,
            [
                new DashboardColumn(first.Id)
                {
                    Name = first.Name,
                    Size = first.Size,
                    Widgets =
                    [
                        new Widget(Guid.NewGuid())
                        {
                            Type = "widget_kleenestar_note",
                            Name = "My Note",
                            Color = "#abcdef",
                            Params = "{\"text\":\"hello\",\"tone\":\"success\"}"
                        }
                    ]
                }
            ]);

            var loaded = CoreHub.InsightManager.GetInsight(insight.Id);
            var column = loaded.Columns.Single(c => c.Id == first.Id);
            var widget = Assert.Single(column.Widgets);

            Assert.Equal("widget_kleenestar_note", widget.Type);
            Assert.Equal("My Note", widget.Name);
            Assert.Equal("#abcdef", widget.Color);
            Assert.Contains("success", widget.Params);
        }

        /// <summary>
        /// Verifies that a session-new column (identified only by its transient client key) is
        /// correlated across a column update and a later board update, so it is not duplicated, and
        /// that a subsequent column-only update preserves the widget a board update added to it.
        /// </summary>
        [Fact]
        public void ClientKey_CorrelatesNewColumnAndPreservesWidgets()
        {
            Seed(nameof(ClientKey_CorrelatesNewColumnAndPreservesWidgets));

            var insight = SampleWithBoard("Board");
            CoreHub.InsightManager.Add(insight);

            var first = insight.Columns[0];
            var second = insight.Columns[1];
            const string clientKey = "col_session_new";

            // add a new column via a column-only update (empty id, transient client key)
            CoreHub.InsightManager.SetColumns(insight.Id,
            [
                new DashboardColumn(first.Id) { Name = first.Name, Size = "1fr" },
                new DashboardColumn(second.Id) { Name = second.Name, Size = "1fr" },
                new DashboardColumn(Guid.Empty) { Key = clientKey, Name = "New column", Size = "1fr" }
            ]);

            Assert.Equal(3, CoreHub.InsightManager.GetInsight(insight.Id).Columns.Count);

            // add a widget to the still-transient column via a board update (client still uses the key)
            CoreHub.InsightManager.SetBoard(insight.Id,
            [
                new DashboardColumn(first.Id) { Name = first.Name, Size = "1fr", Widgets = [WidgetOf("widget_info", "Left")] },
                new DashboardColumn(second.Id) { Name = second.Name, Size = "1fr", Widgets = [WidgetOf("widget_info", "Right")] },
                new DashboardColumn(Guid.Empty) { Key = clientKey, Name = "New column", Size = "1fr", Widgets = [WidgetOf("widget_kleenestar_note", "Note")] }
            ]);

            var afterBoard = CoreHub.InsightManager.GetInsight(insight.Id);
            Assert.Equal(3, afterBoard.Columns.Count);
            Assert.Single(afterBoard.Columns.Single(c => c.Key == clientKey).Widgets);

            // a later column-only update (reorder) must keep the keyed column's widget
            CoreHub.InsightManager.SetColumns(insight.Id,
            [
                new DashboardColumn(Guid.Empty) { Key = clientKey, Name = "New column", Size = "1fr" },
                new DashboardColumn(first.Id) { Name = first.Name, Size = "1fr" },
                new DashboardColumn(second.Id) { Name = second.Name, Size = "1fr" }
            ]);

            var afterReorder = CoreHub.InsightManager.GetInsight(insight.Id);
            Assert.Equal(3, afterReorder.Columns.Count);
            var keyed = afterReorder.Columns.Single(c => c.Key == clientKey);
            Assert.Single(keyed.Widgets);
            Assert.Equal(0, keyed.Position);
        }

        /// <summary>
        /// Creates a detached widget of the given type and name for use in board test payloads.
        /// </summary>
        /// <param name="type">The widget registry type id.</param>
        /// <param name="name">The widget name.</param>
        /// <returns>The widget.</returns>
        private static Widget WidgetOf(string type, string name) => new(Guid.NewGuid())
        {
            Type = type,
            Name = name
        };

        /// <summary>
        /// Verifies that a tab added under a name the insight already carries is numbered, and
        /// that tabs are appended behind the ones there are.
        /// </summary>
        [Fact]
        public void AddView_NumbersATakenNameAndAppends()
        {
            Seed(nameof(AddView_NumbersATakenNameAndAppends));

            var insight = Sample("Tabs");
            CoreHub.InsightManager.Add(insight);

            CoreHub.InsightManager.AddView(new InsightView { InsightId = insight.Id, Name = "Objects", ViewType = InsightViewTypes.Objects });
            CoreHub.InsightManager.AddView(new InsightView { InsightId = insight.Id, Name = "Objects", ViewType = InsightViewTypes.Objects });

            var views = CoreHub.InsightManager.GetViews(insight.Id);

            Assert.Equal(["Objects", "Objects (2)"], views.Select(x => x.Name));
            Assert.Equal([0, 1], views.Select(x => x.Order));
        }

        /// <summary>
        /// Verifies that a new insight gets the objects and the reports tab, and that an insight
        /// that has tabs is left alone.
        /// </summary>
        [Fact]
        public void AddDefaultViews_OnlyOnAnInsightWithoutTabs()
        {
            Seed(nameof(AddDefaultViews_OnlyOnAnInsightWithoutTabs));

            var insight = Sample("Defaults");
            CoreHub.InsightManager.Add(insight);

            CoreHub.InsightManager.AddDefaultViews(insight.Id, key => key);
            CoreHub.InsightManager.AddDefaultViews(insight.Id, key => key);

            Assert.Equal
            (
                [InsightViewTypes.Objects, InsightViewTypes.Reports],
                CoreHub.InsightManager.GetViews(insight.Id).Select(x => x.ViewType)
            );
        }

        /// <summary>
        /// Verifies that a copy takes the tabs and a board of its own with the same widgets.
        /// </summary>
        [Fact]
        public void CopyViews_CopiesTabsAndBoard()
        {
            Seed(nameof(CopyViews_CopiesTabsAndBoard));

            var source = SampleWithBoard("Source");
            CoreHub.InsightManager.Add(source);
            CoreHub.InsightManager.AddView(new InsightView { InsightId = source.Id, Name = "Board", ViewType = InsightViewTypes.Dashboard });
            CoreHub.InsightManager.AddView(new InsightView { InsightId = source.Id, Name = "Charts", ViewType = InsightViewTypes.Reports });

            var target = new Insight(Guid.NewGuid()) { Name = "Target", Created = DateTime.UtcNow, Updated = DateTime.UtcNow };
            CoreHub.InsightManager.Add(target);

            CoreHub.InsightManager.CopyViews(source.Id, target.Id);

            var copied = CoreHub.InsightManager.GetInsight(target.Id);
            var original = CoreHub.InsightManager.GetInsight(source.Id);

            Assert.Equal(["Board", "Charts"], CoreHub.InsightManager.GetViews(target.Id).Select(x => x.Name));
            Assert.Equal(original.Columns.Count, copied.Columns.Count);
            Assert.Empty(copied.Columns.Select(x => x.Id).Intersect(original.Columns.Select(x => x.Id)));
            Assert.Equal
            (
                original.Columns.SelectMany(x => x.Widgets).Select(x => x.Name).OrderBy(x => x),
                copied.Columns.SelectMany(x => x.Widgets).Select(x => x.Name).OrderBy(x => x)
            );
        }

        /// <summary>
        /// Verifies that removing a tab removes it and nothing else.
        /// </summary>
        [Fact]
        public void RemoveView_RemovesTheTab()
        {
            Seed(nameof(RemoveView_RemovesTheTab));

            var insight = Sample("Remove");
            CoreHub.InsightManager.Add(insight);
            CoreHub.InsightManager.AddDefaultViews(insight.Id, key => key);

            var first = CoreHub.InsightManager.GetViews(insight.Id)[0];

            Assert.True(CoreHub.InsightManager.RemoveView(first.Id));
            Assert.False(CoreHub.InsightManager.RemoveView(first.Id));
            Assert.Single(CoreHub.InsightManager.GetViews(insight.Id));
        }

        /// <summary>
        /// Creates a sample <see cref="Insight"/> with a fresh GUID.
        /// </summary>
        /// <param name="name">The insight name.</param>
        /// <returns>The sample insight.</returns>
        private static Insight Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            State = InsightState.Active
        };

        /// <summary>
        /// Creates a sample <see cref="Insight"/> with two columns, each carrying one widget, so
        /// the column and board persistence paths have a graph to operate on.
        /// </summary>
        /// <param name="name">The insight name.</param>
        /// <returns>The sample insight with a seeded board.</returns>
        private static Insight SampleWithBoard(string name)
        {
            var insight = Sample(name);

            var left = new DashboardColumn(Guid.NewGuid())
            {
                Name = "Left",
                Size = "1fr",
                Position = 0,
                InsightId = insight.Id
            };
            left.Widgets.Add(new Widget(Guid.NewGuid())
            {
                Type = "widget_info",
                Name = "Left Widget",
                Position = 0,
                ColumnId = left.Id
            });

            var right = new DashboardColumn(Guid.NewGuid())
            {
                Name = "Right",
                Size = "1fr",
                Position = 1,
                InsightId = insight.Id
            };
            right.Widgets.Add(new Widget(Guid.NewGuid())
            {
                Type = "widget_info",
                Name = "Right Widget",
                Position = 0,
                ColumnId = right.Id
            });

            insight.Columns = [left, right];

            return insight;
        }
    }
}
