using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectViewManager"/>.
    /// Covers CRUD round-trips, workspace-scoped ordering, and event emission.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectViewManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E1A28F1D-2C7E-4B0F-9C5A-12B34C56D789");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("F2B39A2E-3D8F-5C10-AD6B-23C45D67E890");

        /// <summary>
        /// Seeds the in-memory database with two workspaces against which the
        /// tests can attach object views.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-ov", Name = "main" });
            }

            if (!db.Workspaces.Any(x => x.Id == OtherWorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "ws-ov-2", Name = "secondary" });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>AddObjectView</c> persists the view and that
        /// <c>GetObjectView</c> retrieves it by its business id.
        /// </summary>
        [Fact]
        public void AddObjectView_Then_GetObjectView_RoundTrip()
        {
            Seed(nameof(AddObjectView_Then_GetObjectView_RoundTrip));

            var view = Sample("Board");
            CoreHub.ObjectViewManager.AddObjectView(view);

            var loaded = CoreHub.ObjectViewManager.GetObjectView(view.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Board", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetViewsForWorkspace</c> returns only views attached
        /// to the supplied workspace and orders them by <see cref="ObjectView.Order"/>.
        /// </summary>
        [Fact]
        public void GetViewsForWorkspace_ReturnsOnlyOwnWorkspaceOrderedByOrder()
        {
            Seed(nameof(GetViewsForWorkspace_ReturnsOnlyOwnWorkspaceOrderedByOrder));

            CoreHub.ObjectViewManager.AddObjectView(Sample("Backlog", order: 2));
            CoreHub.ObjectViewManager.AddObjectView(Sample("Board", order: 1));
            CoreHub.ObjectViewManager.AddObjectView(Sample("Other", order: 1, workspaceId: OtherWorkspaceId));

            var result = CoreHub.ObjectViewManager.GetViewsForWorkspace(WorkspaceId).ToList();

            Assert.Equal(2, result.Count);
            Assert.Equal("Board", result[0].Name);
            Assert.Equal("Backlog", result[1].Name);
        }

        /// <summary>
        /// Verifies that <c>UpdateObjectView</c> writes scalar property changes back.
        /// </summary>
        [Fact]
        public void UpdateObjectView_ChangesScalars()
        {
            Seed(nameof(UpdateObjectView_ChangesScalars));

            var view = Sample("Initial");
            CoreHub.ObjectViewManager.AddObjectView(view);

            view.Name = "Renamed";
            view.Order = 42;
            CoreHub.ObjectViewManager.UpdateObjectView(view);

            var loaded = CoreHub.ObjectViewManager.GetObjectView(view.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal(42, loaded.Order);
        }

        /// <summary>
        /// Verifies that <c>RemoveObjectView</c> deletes the view and raises the
        /// <see cref="KleeneStar.Core.WebManager.IObjectViewManager.ObjectViewRemoved"/> event.
        /// </summary>
        [Fact]
        public void RemoveObjectView_DeletesAndRaisesEvent()
        {
            Seed(nameof(RemoveObjectView_DeletesAndRaisesEvent));

            var view = Sample("DeleteMe");
            CoreHub.ObjectViewManager.AddObjectView(view);

            ObjectView raised = null;
            CoreHub.ObjectViewManager.ObjectViewRemoved += (_, v) => raised = v;

            CoreHub.ObjectViewManager.RemoveObjectView(view);

            Assert.Null(CoreHub.ObjectViewManager.GetObjectView(view.Id));
            Assert.NotNull(raised);
            Assert.Equal(view.Id, raised.Id);
        }

        /// <summary>
        /// Creates a sample <see cref="ObjectView"/> attached to one of the seeded workspaces.
        /// </summary>
        /// <param name="name">The view name.</param>
        /// <param name="order">The display order.</param>
        /// <param name="workspaceId">The workspace to attach to; defaults to the primary workspace.</param>
        /// <returns>A new view with a fresh GUID.</returns>
        private static ObjectView Sample(string name, int order = 0, Guid? workspaceId = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ViewType = ObjectViewType.Table,
            Order = order,
            State = ObjectViewState.Active,
            WorkspaceId = workspaceId ?? WorkspaceId
        };
    }
}
