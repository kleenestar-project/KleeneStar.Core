using WebExpress.WebIndex.Queries;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.PriorityManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestPriorityManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("CC22334D-4455-6677-8899-AABB00CCDDEE");
        private static readonly Guid ClassId = Guid.Parse("DD33445E-5566-7788-99AA-BBCC11DDEE00");

        /// <summary>
        /// Seeds the in-memory database with a workspace and class for the priorities.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-pri", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the priority and that <c>GetPriority</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetPriority_RoundTrip()
        {
            Seed(nameof(Add_Then_GetPriority_RoundTrip));

            var priority = Sample("High");
            CoreHub.PriorityManager.Add(priority);

            var loaded = CoreHub.PriorityManager.GetPriority(priority.Id);

            Assert.NotNull(loaded);
            Assert.Equal("High", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetPriorities(ClassIdParameter)</c> returns only priorities
        /// attached to the supplied class.
        /// </summary>
        [Fact]
        public void GetPriorities_ByClassId_ReturnsPrioritiesForClass()
        {
            Seed(nameof(GetPriorities_ByClassId_ReturnsPrioritiesForClass));

            CoreHub.PriorityManager.Add(Sample("Low"));
            CoreHub.PriorityManager.Add(Sample("High"));

            var result = CoreHub.PriorityManager.GetPriorities(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.Name == "Low");
            Assert.Contains(result, p => p.Name == "High");
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var priority = Sample("Initial");
            CoreHub.PriorityManager.Add(priority);

            priority.Name = "Renamed";
            CoreHub.PriorityManager.Update(priority);

            var loaded = CoreHub.PriorityManager.GetPriority(priority.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the priority and raises the
        /// <see cref="KleeneStar.Core.WebManager.IPriorityManager.PriorityRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var priority = Sample("DeleteMe");
            CoreHub.PriorityManager.Add(priority);

            Priority raised = null;
            CoreHub.PriorityManager.PriorityRemoved += (_, p) => raised = p;

            CoreHub.PriorityManager.Remove(priority.Id);

            Assert.Null(CoreHub.PriorityManager.GetPriority(priority.Id));
            Assert.NotNull(raised);
            Assert.Equal(priority.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>Reorder</c> applies the given order, which is what a dragged row set
        /// arrives as.
        /// </summary>
        [Fact]
        public void Reorder_AppliesGivenOrder()
        {
            Seed(nameof(Reorder_AppliesGivenOrder));

            var a = Sample("Alpha");
            var b = Sample("Bravo");
            var c = Sample("Charlie");
            CoreHub.PriorityManager.Add(a);
            CoreHub.PriorityManager.Add(b);
            CoreHub.PriorityManager.Add(c);

            CoreHub.PriorityManager.Reorder([c.Id, a.Id, b.Id]);

            Assert.Equal
            (
                ["Charlie", "Alpha", "Bravo"],
                Ordered().Select(p => p.Name)
            );
        }

        /// <summary>
        /// Verifies that <c>Move</c> swaps a priority with the entry above it.
        /// </summary>
        [Fact]
        public void Move_Up_SwapsWithPredecessor()
        {
            Seed(nameof(Move_Up_SwapsWithPredecessor));

            var a = Sample("Alpha");
            var b = Sample("Bravo");
            CoreHub.PriorityManager.Add(a);
            CoreHub.PriorityManager.Add(b);
            CoreHub.PriorityManager.Reorder([a.Id, b.Id]);

            CoreHub.PriorityManager.Move(b.Id, up: true);

            Assert.Equal(["Bravo", "Alpha"], Ordered().Select(p => p.Name));
        }

        /// <summary>
        /// Verifies that <c>Move</c> swaps a priority with the entry below it.
        /// </summary>
        [Fact]
        public void Move_Down_SwapsWithSuccessor()
        {
            Seed(nameof(Move_Down_SwapsWithSuccessor));

            var a = Sample("Alpha");
            var b = Sample("Bravo");
            CoreHub.PriorityManager.Add(a);
            CoreHub.PriorityManager.Add(b);
            CoreHub.PriorityManager.Reorder([a.Id, b.Id]);

            CoreHub.PriorityManager.Move(a.Id, up: false);

            Assert.Equal(["Bravo", "Alpha"], Ordered().Select(p => p.Name));
        }

        /// <summary>
        /// Verifies that moving beyond either end leaves the order untouched, so a repeated click
        /// cannot wrap an entry around to the other end.
        /// </summary>
        /// <param name="up">The direction to move.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Move_AtBoundary_DoesNothing(bool up)
        {
            Seed(nameof(Move_AtBoundary_DoesNothing) + up);

            var a = Sample("Alpha");
            var b = Sample("Bravo");
            CoreHub.PriorityManager.Add(a);
            CoreHub.PriorityManager.Add(b);
            CoreHub.PriorityManager.Reorder([a.Id, b.Id]);

            CoreHub.PriorityManager.Move(up ? a.Id : b.Id, up);

            Assert.Equal(["Alpha", "Bravo"], Ordered().Select(p => p.Name));
        }

        /// <summary>
        /// Verifies that moving an unknown priority is a no-op.
        /// </summary>
        [Fact]
        public void Move_UnknownId_DoesNothing()
        {
            Seed(nameof(Move_UnknownId_DoesNothing));

            CoreHub.PriorityManager.Add(Sample("Alpha"));

            CoreHub.PriorityManager.Move(Guid.NewGuid(), up: true);

            Assert.Equal(["Alpha"], Ordered().Select(p => p.Name));
        }

        /// <summary>
        /// Returns the priorities of the test class in their display order.
        /// </summary>
        /// <returns>The ordered priorities.</returns>
        private static IEnumerable<Priority> Ordered()
        {
            return CoreHub.PriorityManager
                .GetPriorities(new Query<Priority>())
                .Where(p => p.ClassId == ClassId)
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Name)
                .ToList();
        }

        /// <summary>
        /// Creates a sample <see cref="Priority"/> attached to the seeded class.
        /// </summary>
        /// <param name="name">The priority name.</param>
        /// <returns>The sample priority.</returns>
        private static Priority Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClassId = ClassId,
            State = PriorityState.Active
        };
    }
}
