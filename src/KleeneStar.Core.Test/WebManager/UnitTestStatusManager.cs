using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.StatusManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestStatusManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("11445566-7788-99AA-BBCC-DD00112233CC");
        private static readonly Guid ClassId = Guid.Parse("22556677-8899-AABB-CCDD-EE11223344DD");
        private static readonly Guid CategoryId = Guid.Parse("33667788-99AA-BBCC-DDEE-FF22334455EE");

        /// <summary>
        /// Seeds the in-memory database with a workspace, a class and a status category.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-st", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.StatusCategories.Any(x => x.Id == CategoryId))
            {
                db.StatusCategories.Add(new StatusCategory(CategoryId) { Name = "To Do", Color = "#888" });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the status and that <c>GetStatus</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetStatus_RoundTrip()
        {
            Seed(nameof(Add_Then_GetStatus_RoundTrip));

            var status = Sample("Open");
            CoreHub.StatusManager.Add(status);

            var loaded = CoreHub.StatusManager.GetStatus(status.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Open", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetStatuses(ClassIdParameter)</c> returns only statuses
        /// attached to the supplied class.
        /// </summary>
        [Fact]
        public void GetStatuses_ByClassId_ReturnsStatusesForClass()
        {
            Seed(nameof(GetStatuses_ByClassId_ReturnsStatusesForClass));

            CoreHub.StatusManager.Add(Sample("Open"));
            CoreHub.StatusManager.Add(Sample("Closed"));

            var result = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, s => s.Name == "Open");
            Assert.Contains(result, s => s.Name == "Closed");
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var status = Sample("Initial");
            CoreHub.StatusManager.Add(status);

            status.Name = "Renamed";
            CoreHub.StatusManager.Update(status);

            var loaded = CoreHub.StatusManager.GetStatus(status.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the status and raises the
        /// <see cref="KleeneStar.Core.WebManager.IStatusManager.StatusRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var status = Sample("DeleteMe");
            CoreHub.StatusManager.Add(status);

            Status raised = null;
            CoreHub.StatusManager.StatusRemoved += (_, s) => raised = s;

            CoreHub.StatusManager.Remove(status.Id);

            Assert.Null(CoreHub.StatusManager.GetStatus(status.Id));
            Assert.NotNull(raised);
            Assert.Equal(status.Id, raised.Id);
        }

        /// <summary>
        /// Creates a sample <see cref="Status"/> attached to the seeded class and category.
        /// </summary>
        /// <param name="name">The status name.</param>
        /// <returns>The sample status.</returns>
        private static Status Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClassId = ClassId,
            CategoryId = CategoryId,
            State = StatusState.Active
        };
    }
}
