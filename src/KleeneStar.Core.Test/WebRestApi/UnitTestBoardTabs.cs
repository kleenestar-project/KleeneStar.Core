using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests independent board configurations and the ownership of tab-scoped requests.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestBoardTabs
    {
        /// <summary>
        /// Verifies that columns, widgets, lanes and filters survive independently per overview tab.
        /// </summary>
        /// <param name="kind">The object kind of the overview.</param>
        [Theory]
        [InlineData("issue")]
        [InlineData("asset")]
        public void WorkspaceBoardsRemainIndependent(string kind)
        {
            // arrange
            CoreHubFixture.Initialize(nameof(WorkspaceBoardsRemainIndependent) + kind);
            var owner = Guid.NewGuid();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            var dashboard = CoreHub.KindDashboardManager.EnsureBoard(owner, kind, first);
            var otherDashboard = CoreHub.KindDashboardManager.EnsureBoard(owner, kind, second);
            var kanban = CoreHub.KanbanBoardManager.EnsureBoard(owner, kind, first);
            var otherKanban = CoreHub.KanbanBoardManager.EnsureBoard(owner, kind, second);

            // act
            CoreHub.KindDashboardManager.SetBoard(dashboard.Id,
                [new KindDashboardColumn { Name = "First", Widgets = [new KindDashboardWidget { Name = "Note", Type = "widget_kleenestar_note", Params = "{\"text\":\"first\"}" }] }]);
            CoreHub.KindDashboardManager.SetBoard(otherDashboard.Id, [new KindDashboardColumn { Name = "Second" }]);
            CoreHub.KanbanBoardManager.SetColumns(kanban.Id, [new KanbanBoardColumn { Name = "First", Statuses = "[\"new\"]" }]);
            CoreHub.KanbanBoardManager.SetColumns(otherKanban.Id, [new KanbanBoardColumn { Name = "Second" }]);
            CoreHub.KanbanBoardManager.SetSwimlanes(kanban.Id, [new KanbanBoardSwimlane { Name = "Lane", Filter = "Summary = 'first'" }]);
            CoreHub.KanbanBoardManager.SetFilter(kanban.Id, "Summary = 'first'");

            // validation
            var saved = CoreHub.KindDashboardManager.GetBoard(owner, kind, first);
            Assert.Equal("First", Assert.Single(saved.Columns).Name);
            Assert.Equal("{\"text\":\"first\"}", Assert.Single(saved.Columns[0].Widgets).Params);
            Assert.Equal("Second", Assert.Single(CoreHub.KindDashboardManager.GetBoard(owner, kind, second).Columns).Name);
            Assert.Equal("First", Assert.Single(CoreHub.KanbanBoardManager.GetBoard(owner, kind, first).Columns).Name);
            Assert.Single(CoreHub.KanbanBoardManager.GetBoard(owner, kind, first).Swimlanes);
            var untouched = CoreHub.KanbanBoardManager.GetBoard(owner, kind, second);
            Assert.Equal("Second", Assert.Single(untouched.Columns).Name);
            Assert.Empty(untouched.Swimlanes);
            Assert.Null(untouched.Filter);
            Assert.Null(CoreHub.KindDashboardManager.GetBoard(owner, kind));
            Assert.Null(CoreHub.KanbanBoardManager.GetBoard(owner, kind));
        }

        /// <summary>
        /// Verifies that a dashboard update and deletion preserve the other insight tabs.
        /// </summary>
        [Fact]
        public void InsightBoardsCloneAndDeleteIndependently()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(InsightBoardsCloneAndDeleteIndependently));
            var source = new Insight { Name = "Source" };
            var target = new Insight { Name = "Copy" };
            CoreHub.InsightManager.Add(source);
            CoreHub.InsightManager.Add(target);
            var first = new InsightView { InsightId = source.Id, Name = "First", ViewType = InsightViewTypes.Dashboard };
            var second = new InsightView { InsightId = source.Id, Name = "Second", ViewType = InsightViewTypes.Dashboard };
            var kanban = new InsightView { InsightId = source.Id, Name = "Kanban", ViewType = InsightViewTypes.Kanban };
            CoreHub.InsightManager.AddView(first).AddView(second).AddView(kanban);
            CoreHub.InsightManager.SetBoard(source.Id, [new DashboardColumn { Name = "First", Widgets = [new Widget { Name = "Note", Wql = "Summary", Params = "{}" }] }], first.Id);
            CoreHub.InsightManager.SetBoard(source.Id, [new DashboardColumn { Name = "Second" }], second.Id);
            var board = CoreHub.KanbanBoardManager.EnsureBoard(source.Id, "insight", kanban.Id);
            CoreHub.KanbanBoardManager.SetColumns(board.Id, [new KanbanBoardColumn { Name = "Column", Statuses = "[\"new\"]" }]);
            CoreHub.KanbanBoardManager.SetSwimlanes(board.Id, [new KanbanBoardSwimlane { Name = "Lane", Filter = "Summary = 'test'" }]);
            CoreHub.KanbanBoardManager.SetFilter(board.Id, "Summary = 'test'");

            // act
            CoreHub.InsightManager.CopyViews(source.Id, target.Id);
            CoreHub.InsightManager.SetColumns(source.Id, [], first.Id);
            CoreHub.InsightManager.RemoveView(kanban.Id);

            // validation
            var sourceColumns = CoreHub.InsightManager.GetInsight(source.Id).Columns;
            Assert.Equal(second.Id, Assert.Single(sourceColumns).ViewId);
            Assert.Equal("Second", sourceColumns[0].Name);
            var copiedViews = CoreHub.InsightManager.GetViews(target.Id);
            var copiedColumns = CoreHub.InsightManager.GetInsight(target.Id).Columns;
            Assert.Equal(2, copiedColumns.Count);
            var copiedFirst = copiedColumns.Single(x => x.ViewId == copiedViews.Single(v => v.Name == "First").Id);
            Assert.Equal("Summary", Assert.Single(copiedFirst.Widgets).Wql);
            Assert.Equal("{}", copiedFirst.Widgets[0].Params);
            var copiedBoard = CoreHub.KanbanBoardManager.GetBoard(target.Id, "insight", copiedViews.Single(v => v.Name == "Kanban").Id);
            Assert.NotEqual(board.Id, copiedBoard.Id);
            Assert.Equal("Summary = 'test'", copiedBoard.Filter);
            Assert.Equal("[\"new\"]", Assert.Single(copiedBoard.Columns).Statuses);
            Assert.Equal("Summary = 'test'", Assert.Single(copiedBoard.Swimlanes).Filter);
            Assert.Null(CoreHub.KanbanBoardManager.GetBoard(source.Id, "insight", kanban.Id));
        }

        /// <summary>
        /// Verifies that tab deletion removes its board graph while retaining sibling boards.
        /// </summary>
        [Fact]
        public void WorkspaceTabDeletionRemovesOnlyItsBoard()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(WorkspaceTabDeletionRemovesOnlyItsBoard));
            var owner = Guid.NewGuid();
            var view = new ObjectView { WorkspaceId = owner, Name = "Board", Kind = "issue", ViewType = ObjectViewType.Kanban };
            CoreHub.ObjectViewManager.AddObjectView(view);
            var first = CoreHub.KanbanBoardManager.EnsureBoard(owner, "issue", view.Id);
            var second = CoreHub.KanbanBoardManager.EnsureBoard(owner, "issue", Guid.NewGuid());
            CoreHub.KanbanBoardManager.SetColumns(first.Id, [new KanbanBoardColumn { Name = "Deleted" }]);

            // act
            CoreHub.ObjectViewManager.RemoveObjectView(view);

            // validation
            Assert.Null(CoreHub.KanbanBoardManager.GetBoard(owner, "issue", view.Id));
            Assert.Equal(second.Id, CoreHub.KanbanBoardManager.GetBoard(owner, "issue", second.ViewId).Id);
        }

        /// <summary>
        /// Verifies that explicit invalid tab identifiers cannot fall back to a shared board.
        /// </summary>
        /// <param name="value">The invalid request parameter.</param>
        [Theory]
        [InlineData("invalid")]
        [InlineData("")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void InvalidTabIsRefused(string value)
        {
            // arrange
            var request = Request(value);

            // act
            var refusal = Record.Exception(() => BoardViewScope.Workspace(request, Guid.NewGuid(), "issue", ObjectViewType.Kanban));

            // validation
            Assert.IsType<RestApiRefusal>(refusal);
        }

        /// <summary>
        /// Verifies workspace, insight, kind, type and lifecycle validation for board requests.
        /// </summary>
        [Fact]
        public void ForeignOrWrongTypeTabsAreRefused()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(ForeignOrWrongTypeTabsAreRefused));
            var view = new ObjectView { WorkspaceId = Guid.NewGuid(), Name = "Board", Kind = "issue", ViewType = ObjectViewType.Kanban };
            CoreHub.WorkspaceManager.Add(new Workspace { Id = view.WorkspaceId, Name = "Workspace", Key = "board" });
            CoreHub.ObjectViewManager.AddObjectView(view);
            var insight = new Insight { Name = "Insight" };
            CoreHub.InsightManager.Add(insight);
            var insightView = new InsightView { InsightId = insight.Id, Name = "Board", ViewType = InsightViewTypes.Kanban };
            CoreHub.InsightManager.AddView(insightView);

            // act
            var workspaceRequest = Request(view.Id.ToString());
            var insightRequest = Request(insightView.Id.ToString());

            // validation
            Assert.Equal(view.Id, BoardViewScope.Workspace(workspaceRequest, view.WorkspaceId, "issue", ObjectViewType.Kanban));
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Workspace(workspaceRequest, Guid.NewGuid(), "issue", ObjectViewType.Kanban));
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Workspace(workspaceRequest, view.WorkspaceId, "asset", ObjectViewType.Kanban));
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Workspace(workspaceRequest, view.WorkspaceId, "issue", ObjectViewType.Dashboard));
            Assert.Equal(insightView.Id, BoardViewScope.Insight(insightRequest, insight.Id, InsightViewTypes.Kanban));
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Insight(insightRequest, Guid.NewGuid(), InsightViewTypes.Kanban));
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Insight(insightRequest, insight.Id, InsightViewTypes.Dashboard));
            CoreHub.InsightManager.RemoveView(insightView.Id);
            Assert.Throws<RestApiRefusal>(() => BoardViewScope.Insight(insightRequest, insight.Id, InsightViewTypes.Kanban));
        }

        /// <summary>
        /// Creates an HTTP request containing the selected board tab identifier.
        /// </summary>
        /// <param name="view">The tab query parameter value.</param>
        /// <returns>The request used by scope validation.</returns>
        private static IRequest Request(string view)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET", Protocol = "HTTP/1.1", Scheme = "http", RawTarget = "/?v=" + view,
                QueryString = "?v=" + view, Headers = new HeaderDictionary { ["Host"] = "localhost" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback, RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature { TraceIdentifier = nameof(UnitTestBoardTabs) });
            return new WebExpress.WebCore.WebMessage.HttpContext(features, null).Request;
        }
    }
}
