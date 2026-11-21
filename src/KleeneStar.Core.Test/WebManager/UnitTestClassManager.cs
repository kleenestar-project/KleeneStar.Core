using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ClassManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("12A45D6E-7F88-49BB-A0C3-1A56B7C89DEE");

        /// <summary>
        /// Seeds the in-memory database with a workspace to which the test classes attach.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-cls", Name = "main" });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the class and that <c>GetClass</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetClass_RoundTrip()
        {
            Seed(nameof(Add_Then_GetClass_RoundTrip));

            var classEntity = Sample("Incident");
            CoreHub.ClassManager.Add(classEntity);

            var loaded = CoreHub.ClassManager.GetClass(classEntity.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Incident", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetClass(ClassIdParameter)</c> resolves the same class as
        /// <c>GetClass(Guid)</c>.
        /// </summary>
        [Fact]
        public void GetClass_ByParameter_ReturnsMatch()
        {
            Seed(nameof(GetClass_ByParameter_ReturnsMatch));

            var classEntity = Sample("Task");
            CoreHub.ClassManager.Add(classEntity);

            var loaded = CoreHub.ClassManager.GetClass(new ClassIdParameter(classEntity.Id));

            Assert.NotNull(loaded);
            Assert.Equal(classEntity.Id, loaded.Id);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var classEntity = Sample("Initial");
            CoreHub.ClassManager.Add(classEntity);

            classEntity.Name = "Renamed";
            CoreHub.ClassManager.Update(classEntity);

            var loaded = CoreHub.ClassManager.GetClass(classEntity.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the class and raises the
        /// <see cref="KleeneStar.Core.WebManager.IClassManager.ClassRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var classEntity = Sample("Delete");
            CoreHub.ClassManager.Add(classEntity);

            Class raised = null;
            CoreHub.ClassManager.ClassRemoved += (_, c) => raised = c;

            CoreHub.ClassManager.Remove(classEntity.Id);

            Assert.Null(CoreHub.ClassManager.GetClass(classEntity.Id));
            Assert.NotNull(raised);
            Assert.Equal(classEntity.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetClasses(IQuery)</c> returns matching classes from the database.
        /// </summary>
        [Fact]
        public void GetClasses_ByQuery_ReturnsMatches()
        {
            Seed(nameof(GetClasses_ByQuery_ReturnsMatches));

            CoreHub.ClassManager.Add(Sample("Alpha"));
            CoreHub.ClassManager.Add(Sample("Beta"));

            var query = new Query<Class>().Where(x => x.WorkspaceId == WorkspaceId);
            var result = CoreHub.ClassManager.GetClasses(query).ToList();

            Assert.True(result.Count >= 2);
            Assert.Contains(result, c => c.Name == "Alpha");
            Assert.Contains(result, c => c.Name == "Beta");
        }

        /// <summary>
        /// Creates a sample <see cref="Class"/> attached to the seeded workspace.
        /// </summary>
        /// <param name="name">The class name.</param>
        /// <returns>The sample class.</returns>
        private static Class Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            WorkspaceId = WorkspaceId,
            State = ClassState.Active
        };
    }
}
