using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests adding, reordering and the actions of the tab menu - rename, color, delete - on the tabs of a workspace
    /// overview (<see cref="ObjectViewTabs"/>) and of an insight (<see cref="WebManager.IInsightManager"/>).
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTabMenu
    {
        /// <summary>
        /// Verifies that a tab is renamed and colored, that a taken name is refused instead of
        /// suffixed, and that a color is stored lower-cased and can be cleared.
        /// </summary>
        [Fact]
        public void WorkspaceTabIsRenamedAndColored()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(WorkspaceTabIsRenamedAndColored));
            var workspace = new Workspace { Id = Guid.NewGuid(), Name = "Tabs", Key = "TABS" };
            CoreHub.WorkspaceManager.Add(workspace);
            var first = new ObjectView { WorkspaceId = workspace.Id, Name = "Table", Kind = "issue", ViewType = ObjectViewType.Table };
            var second = new ObjectView { WorkspaceId = workspace.Id, Name = "Board", Kind = "issue", ViewType = ObjectViewType.Kanban };
            CoreHub.ObjectViewManager.AddObjectView(first).AddObjectView(second);
            var request = Request("TABS");

            // act
            var renamed = ObjectViewTabs.Rename(first.Id.ToString(), "Mine", "issue", request);
            var taken = ObjectViewTabs.Rename(first.Id.ToString(), "board", "issue", request);
            var colored = ObjectViewTabs.Recolor(first.Id.ToString(), "#1A2B3C", "issue", request);

            // validation
            Assert.True(renamed);
            Assert.False(taken);
            Assert.True(colored);
            var stored = CoreHub.ObjectViewManager.GetObjectView(first.Id);
            Assert.Equal("Mine", stored.Name);
            Assert.Equal("#1a2b3c", stored.Color);
            Assert.Null(CoreHub.ObjectViewManager.GetObjectView(second.Id).Color);

            Assert.True(ObjectViewTabs.Recolor(first.Id.ToString(), null, "issue", request));
            Assert.Null(CoreHub.ObjectViewManager.GetObjectView(first.Id).Color);
        }

        /// <summary>
        /// Verifies that a tab is reachable only through the route of its own workspace and kind.
        /// </summary>
        [Fact]
        public void ForeignWorkspaceTabIsNotChanged()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(ForeignWorkspaceTabIsNotChanged));
            var own = new Workspace { Id = Guid.NewGuid(), Name = "Own", Key = "OWN" };
            var other = new Workspace { Id = Guid.NewGuid(), Name = "Other", Key = "OTHER" };
            CoreHub.WorkspaceManager.Add(own);
            CoreHub.WorkspaceManager.Add(other);
            var foreign = new ObjectView { WorkspaceId = other.Id, Name = "Table", Kind = "issue", ViewType = ObjectViewType.Table };
            var asset = new ObjectView { WorkspaceId = own.Id, Name = "Assets", Kind = "asset", ViewType = ObjectViewType.Table };
            CoreHub.ObjectViewManager.AddObjectView(foreign).AddObjectView(asset);
            var request = Request("OWN");

            // act
            var renamedForeign = ObjectViewTabs.Rename(foreign.Id.ToString(), "Mine", "issue", request);
            var coloredWrongKind = ObjectViewTabs.Recolor(asset.Id.ToString(), "#198754", "issue", request);
            var removedForeign = ObjectViewTabs.Remove(foreign.Id.ToString(), "issue", request);
            var removedOwn = ObjectViewTabs.Remove(asset.Id.ToString(), "asset", request);

            // validation
            Assert.False(renamedForeign);
            Assert.False(coloredWrongKind);
            Assert.False(removedForeign);
            Assert.True(removedOwn);
            Assert.Equal("Table", CoreHub.ObjectViewManager.GetObjectView(foreign.Id).Name);
            Assert.Null(CoreHub.ObjectViewManager.GetObjectView(asset.Id));
        }

        /// <summary>
        /// Verifies that the tabs of an overview are put into the dragged order, that an id of
        /// another workspace is ignored and that the tabs of the other kind keep their order.
        /// </summary>
        [Fact]
        public void WorkspaceTabsAreReordered()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(WorkspaceTabsAreReordered));
            var workspace = new Workspace { Id = Guid.NewGuid(), Name = "Order", Key = "ORDER" };
            CoreHub.WorkspaceManager.Add(workspace);
            var a = new ObjectView { WorkspaceId = workspace.Id, Name = "A", Kind = "issue", ViewType = ObjectViewType.Table, Order = 0 };
            var b = new ObjectView { WorkspaceId = workspace.Id, Name = "B", Kind = "issue", ViewType = ObjectViewType.List, Order = 1 };
            var c = new ObjectView { WorkspaceId = workspace.Id, Name = "C", Kind = "issue", ViewType = ObjectViewType.Kanban, Order = 2 };
            var asset = new ObjectView { WorkspaceId = workspace.Id, Name = "Assets", Kind = "asset", ViewType = ObjectViewType.Table, Order = 0 };
            CoreHub.ObjectViewManager.AddObjectView(a).AddObjectView(b).AddObjectView(c).AddObjectView(asset);
            var request = Request("ORDER");

            // act
            var reordered = ObjectViewTabs.Reorder([c.Id.ToString(), Guid.NewGuid().ToString(), "invalid", a.Id.ToString()], "issue", request);
            var nothingNamed = ObjectViewTabs.Reorder([asset.Id.ToString()], "issue", request);

            // validation
            Assert.True(reordered);
            Assert.False(nothingNamed);
            Assert.Equal(["C", "A", "B"], CoreHub.ObjectViewManager.GetViewsForWorkspace(workspace.Id, "issue").Select(x => x.Name));
            Assert.Equal(0, CoreHub.ObjectViewManager.GetObjectView(asset.Id).Order);
        }

        /// <summary>
        /// Verifies that once a workspace is administered, a caller outside every granted group -
        /// here an anonymous one - may neither add, reorder, rename, color nor delete its tabs.
        /// </summary>
        [Fact]
        public void AdministeredWorkspaceRefusesAnonymousArrangement()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(AdministeredWorkspaceRefusesAnonymousArrangement));
            var workspace = new Workspace { Id = Guid.NewGuid(), Name = "Closed", Key = "CLOSED" };
            CoreHub.WorkspaceManager.Add(workspace);
            var view = new ObjectView { WorkspaceId = workspace.Id, Name = "Table", Kind = "issue", ViewType = ObjectViewType.Table };
            CoreHub.ObjectViewManager.AddObjectView(view);
            var request = Request("CLOSED");
            Assert.True(ObjectViewTabs.MayArrange(request));

            using (var db = CoreHubFixture.CreateDbContext(KleeneStar.Model.ModelHub.DatabaseSettings.ConnectionString))
            {
                var group = new Group { Id = Guid.NewGuid(), Name = "Editors" };
                db.Groups.Add(group);
                db.PermissionAssignments.Add(new PermissionAssignment
                {
                    Id = Guid.NewGuid(),
                    Scope = PermissionScope.Workspace,
                    ScopeId = workspace.Id.ToString(),
                    GroupId = group.Id,
                    Policy = "workspace_admin_policy",
                    Created = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            request = Request("CLOSED");

            // act
            var refusal = Record.Exception(() => ObjectViewTabs.DemandArrange(request));

            // validation
            Assert.False(ObjectViewTabs.MayArrange(request));
            Assert.IsType<RestApiRefusal>(refusal);
            Assert.False(ObjectViewTabs.Reorder([view.Id.ToString()], "issue", request));
            Assert.False(ObjectViewTabs.Rename(view.Id.ToString(), "Mine", "issue", request));
            Assert.False(ObjectViewTabs.Recolor(view.Id.ToString(), "#198754", "issue", request));
            Assert.False(ObjectViewTabs.Remove(view.Id.ToString(), "issue", request));
            Assert.NotNull(CoreHub.ObjectViewManager.GetObjectView(view.Id));
        }

        /// <summary>
        /// Verifies that an insight tab is renamed and colored, that a taken name is refused,
        /// and that a cloned insight keeps the colors of its tabs.
        /// </summary>
        [Fact]
        public void InsightTabIsRenamedColoredAndCopied()
        {
            // arrange
            CoreHubFixture.Initialize(nameof(InsightTabIsRenamedColoredAndCopied));
            var source = new Insight { Name = "Source" };
            var target = new Insight { Name = "Copy" };
            CoreHub.InsightManager.Add(source);
            CoreHub.InsightManager.Add(target);
            var first = new InsightView { InsightId = source.Id, Name = "Objects", ViewType = InsightViewTypes.Objects };
            var second = new InsightView { InsightId = source.Id, Name = "Reports", ViewType = InsightViewTypes.Reports };
            CoreHub.InsightManager.AddView(first).AddView(second);

            // act
            var renamed = CoreHub.InsightManager.RenameView(first.Id, " Open work ");
            var taken = CoreHub.InsightManager.RenameView(first.Id, "REPORTS");
            var colored = CoreHub.InsightManager.SetViewColor(first.Id, "#DC3545");
            CoreHub.InsightManager.CopyViews(source.Id, target.Id);

            // validation
            Assert.True(renamed);
            Assert.False(taken);
            Assert.True(colored);
            var stored = CoreHub.InsightManager.GetView(first.Id);
            Assert.Equal("Open work", stored.Name);
            Assert.Equal("#dc3545", stored.Color);
            Assert.Equal("#dc3545", CoreHub.InsightManager.GetViews(target.Id).Single(x => x.Name == "Open work").Color);
            Assert.False(CoreHub.InsightManager.SetViewColor(Guid.NewGuid(), "#dc3545"));
        }

        /// <summary>
        /// Creates a request whose route names a workspace.
        /// </summary>
        /// <param name="workspaceKey">The key of the workspace.</param>
        /// <returns>The request.</returns>
        private static IRequest Request(string workspaceKey)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "PUT",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/api/1/objects/" + workspaceKey + "/tab",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestTabMenu)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                WorkspaceKeyParameter.Key,
                workspaceKey,
                WebExpress.WebCore.WebParameter.ParameterScope.Url));

            return request;
        }
    }
}
