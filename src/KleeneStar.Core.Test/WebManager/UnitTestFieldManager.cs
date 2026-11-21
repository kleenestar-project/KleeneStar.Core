using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.FieldManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestFieldManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("AA00112E-2233-4455-6677-88990011AABB");
        private static readonly Guid ClassId = Guid.Parse("BB11223F-3344-5566-7788-9900AABBCCDD");

        /// <summary>
        /// Seeds the in-memory database with a workspace and class to which fields can attach.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-fld", Name = "main" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the field and that <c>GetField</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetField_RoundTrip()
        {
            Seed(nameof(Add_Then_GetField_RoundTrip));

            var field = Sample("Severity");
            CoreHub.FieldManager.Add(field);

            var loaded = CoreHub.FieldManager.GetField(field.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Severity", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetFields(ClassIdParameter)</c> returns only fields belonging
        /// to the supplied class.
        /// </summary>
        [Fact]
        public void GetFields_ByClassId_ReturnsFieldsForClass()
        {
            Seed(nameof(GetFields_ByClassId_ReturnsFieldsForClass));

            CoreHub.FieldManager.Add(Sample("Severity"));
            CoreHub.FieldManager.Add(Sample("Priority"));

            var result = CoreHub.FieldManager.GetFields(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, f => f.Name == "Severity");
            Assert.Contains(result, f => f.Name == "Priority");
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var field = Sample("Initial");
            CoreHub.FieldManager.Add(field);

            field.Name = "Renamed";
            CoreHub.FieldManager.Update(field);

            var loaded = CoreHub.FieldManager.GetField(field.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the field and raises the
        /// <see cref="KleeneStar.Core.WebManager.IFieldManager.FieldRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var field = Sample("DeleteMe");
            CoreHub.FieldManager.Add(field);

            Field raised = null;
            CoreHub.FieldManager.FieldRemoved += (_, f) => raised = f;

            CoreHub.FieldManager.Remove(field.Id);

            Assert.Null(CoreHub.FieldManager.GetField(field.Id));
            Assert.NotNull(raised);
            Assert.Equal(field.Id, raised.Id);
        }

        /// <summary>
        /// Creates a sample <see cref="Field"/> attached to the seeded class.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <returns>The sample field.</returns>
        private static Field Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClassId = ClassId,
            State = FieldState.Active
        };
    }
}
