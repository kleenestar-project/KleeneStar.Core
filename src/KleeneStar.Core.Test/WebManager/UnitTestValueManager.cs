using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ValueManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestValueManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("11AB31E4-7C1D-4B11-A8C5-9B3F5A4C8B22");
        private static readonly Guid ClassId = Guid.Parse("22BC42F5-8D2E-4C22-B9D6-AC4F6B5D9C33");
        private static readonly Guid ObjectId = Guid.Parse("33CD53F6-9E3F-4D33-CAE7-BD506C6EAD44");
        private static readonly Guid FieldId = Guid.Parse("44DE64F7-AF40-4E44-DBF8-CE617D7FBE55");

        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-vm", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new KleeneStar.Model.Entities.Object { Id = ObjectId, Key = "INC-200", Summary = "Test", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }
            if (!db.Fields.Any(x => x.Id == FieldId))
            {
                db.Fields.Add(new Field { Id = FieldId, Name = "Severity", ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add → GetValue round-trip.
        /// </summary>
        [Fact]
        public void Add_Then_GetValue_RoundTrip()
        {
            Seed(nameof(Add_Then_GetValue_RoundTrip));

            var value = SampleValue("high");
            CoreHub.ValueManager.Add(value);

            var loaded = CoreHub.ValueManager.GetValue(value.Id);

            Assert.NotNull(loaded);
            Assert.Equal("high", loaded.Data);
        }

        /// <summary>
        /// GetValue(objectId, fieldId) resolves the (object, field) pair.
        /// </summary>
        [Fact]
        public void GetValue_ByObjectAndField_ReturnsMatch()
        {
            Seed(nameof(GetValue_ByObjectAndField_ReturnsMatch));

            var value = SampleValue("medium");
            CoreHub.ValueManager.Add(value);

            var loaded = CoreHub.ValueManager.GetValue(ObjectId, FieldId);

            Assert.NotNull(loaded);
            Assert.Equal("medium", loaded.Data);
        }

        /// <summary>
        /// GetValues(objectId) returns all values for the supplied object.
        /// </summary>
        [Fact]
        public void GetValues_ByObjectId_ReturnsValuesForObject()
        {
            Seed(nameof(GetValues_ByObjectId_ReturnsValuesForObject));

            CoreHub.ValueManager.Add(SampleValue("low"));

            var result = CoreHub.ValueManager.GetValues(ObjectId).ToList();

            Assert.Single(result);
            Assert.Equal("low", result[0].Data);
        }

        /// <summary>
        /// Update changes the persisted payload.
        /// </summary>
        [Fact]
        public void Update_ChangesData()
        {
            Seed(nameof(Update_ChangesData));

            var value = SampleValue("initial");
            CoreHub.ValueManager.Add(value);

            value.Data = "updated";
            CoreHub.ValueManager.Update(value);

            var loaded = CoreHub.ValueManager.GetValue(value.Id);
            Assert.NotNull(loaded);
            Assert.Equal("updated", loaded.Data);
        }

        /// <summary>
        /// Remove hard-deletes the value and raises the event.
        /// </summary>
        [Fact]
        public void Remove_HardDeletes_RaisesEvent()
        {
            Seed(nameof(Remove_HardDeletes_RaisesEvent));

            var value = SampleValue("delete me");
            CoreHub.ValueManager.Add(value);

            Value raised = null;
            CoreHub.ValueManager.ValueRemoved += (_, v) => raised = v;

            CoreHub.ValueManager.Remove(value.Id);

            Assert.Null(CoreHub.ValueManager.GetValue(value.Id));
            Assert.NotNull(raised);
            Assert.Equal(value.Id, raised.Id);
        }

        /// <summary>
        /// Remove of an unknown id is a no-op.
        /// </summary>
        [Fact]
        public void Remove_Unknown_IsNoOp()
        {
            Seed(nameof(Remove_Unknown_IsNoOp));

            CoreHub.ValueManager.Remove(Guid.NewGuid());

            Assert.Empty(CoreHub.ValueManager.GetValues(ObjectId));
        }

        private static Value SampleValue(string data) => new()
        {
            Id = Guid.NewGuid(),
            ObjectId = ObjectId,
            FieldId = FieldId,
            Data = data
        };
    }
}
