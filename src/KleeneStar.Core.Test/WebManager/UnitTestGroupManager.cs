using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.GroupManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestGroupManager
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
        /// Verifies that <c>Add</c> persists the group and that <c>GetGroup</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetGroup_RoundTrip()
        {
            Seed(nameof(Add_Then_GetGroup_RoundTrip));

            var group = Sample("Engineering");
            CoreHub.GroupManager.Add(group);

            var loaded = CoreHub.GroupManager.GetGroup(group.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Engineering", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var group = Sample("Initial");
            CoreHub.GroupManager.Add(group);

            group.Name = "Renamed";
            CoreHub.GroupManager.Update(group);

            var loaded = CoreHub.GroupManager.GetGroup(group.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the group and raises the
        /// <see cref="KleeneStar.Core.WebManager.IGroupManager.GroupRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var group = Sample("DeleteMe");
            CoreHub.GroupManager.Add(group);

            Group raised = null;
            CoreHub.GroupManager.GroupRemoved += (_, g) => raised = g;

            CoreHub.GroupManager.Remove(group.Id);

            Assert.Null(CoreHub.GroupManager.GetGroup(group.Id));
            Assert.NotNull(raised);
            Assert.Equal(group.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetGroups(IQuery)</c> returns groups from the database.
        /// </summary>
        [Fact]
        public void GetGroups_ReturnsAllStored()
        {
            Seed(nameof(GetGroups_ReturnsAllStored));

            CoreHub.GroupManager.Add(Sample("Alpha"));
            CoreHub.GroupManager.Add(Sample("Beta"));

            var result = CoreHub.GroupManager.GetGroups(new Query<Group>()).ToList();

            Assert.True(result.Count >= 2);
            Assert.Contains(result, g => g.Name == "Alpha");
            Assert.Contains(result, g => g.Name == "Beta");
        }

        /// <summary>
        /// Creates a sample <see cref="Group"/> with a fresh GUID.
        /// </summary>
        /// <param name="name">The group name.</param>
        /// <returns>The sample group.</returns>
        private static Group Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = name + " description"
        };
    }
}
