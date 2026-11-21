using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("44778899-AABB-CCDD-EEFF-112233445566");
        private static readonly Guid ClassId = Guid.Parse("55889900-BBCC-DDEE-FF00-223344556677");

        /// <summary>
        /// Seeds the in-memory database with the workspace and class objects attach to.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-obj", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the object and that <c>GetObject</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetObject_RoundTrip()
        {
            Seed(nameof(Add_Then_GetObject_RoundTrip));

            var obj = Sample("INC-1", "Server down");
            CoreHub.ObjectManager.Add(obj);

            var loaded = CoreHub.ObjectManager.GetObject(obj.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Server down", loaded.Summary);
        }

        /// <summary>
        /// Verifies that <c>GetObjectByKey</c> resolves the object by its key.
        /// </summary>
        [Fact]
        public void GetObjectByKey_ReturnsMatch()
        {
            Seed(nameof(GetObjectByKey_ReturnsMatch));

            CoreHub.ObjectManager.Add(Sample("INC-42", "Outage"));

            var loaded = CoreHub.ObjectManager.GetObjectByKey("INC-42");

            Assert.NotNull(loaded);
            Assert.Equal("Outage", loaded.Summary);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var obj = Sample("INC-7", "Initial");
            CoreHub.ObjectManager.Add(obj);

            obj.Summary = "Renamed";
            CoreHub.ObjectManager.Update(obj);

            var loaded = CoreHub.ObjectManager.GetObject(obj.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Summary);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the object and raises the
        /// <see cref="KleeneStar.Core.WebManager.IObjectManager.ObjectRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var obj = Sample("INC-99", "DeleteMe");
            CoreHub.ObjectManager.Add(obj);

            ObjectEntity raised = null;
            CoreHub.ObjectManager.ObjectRemoved += (_, o) => raised = o;

            CoreHub.ObjectManager.Remove(obj.Id);

            Assert.Null(CoreHub.ObjectManager.GetObject(obj.Id));
            Assert.NotNull(raised);
            Assert.Equal(obj.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetChildren</c> returns the immediate children of an object
        /// and <c>GetSiblings</c> excludes the reference object itself.
        /// </summary>
        [Fact]
        public void GetChildren_And_GetSiblings_ReturnFamily()
        {
            Seed(nameof(GetChildren_And_GetSiblings_ReturnFamily));

            var parent = Sample("INC-100", "Parent");
            var childA = Sample("INC-101", "ChildA");
            var childB = Sample("INC-102", "ChildB");
            childA.ParentId = parent.Id;
            childB.ParentId = parent.Id;

            CoreHub.ObjectManager.Add(parent);
            CoreHub.ObjectManager.Add(childA);
            CoreHub.ObjectManager.Add(childB);

            var children = CoreHub.ObjectManager.GetChildren(parent.Id).ToList();
            Assert.Equal(2, children.Count);

            var siblings = CoreHub.ObjectManager.GetSiblings(childA.Id).ToList();
            Assert.DoesNotContain(siblings, o => o.Id == childA.Id);
            Assert.Contains(siblings, o => o.Id == childB.Id);
        }

        /// <summary>
        /// Verifies that <c>RecordVisit</c> creates a visit that surfaces in
        /// <c>GetRecentObjects</c>.
        /// </summary>
        [Fact]
        public void RecordVisit_CreatesVisit_AndSurfacesInRecent()
        {
            Seed(nameof(RecordVisit_CreatesVisit_AndSurfacesInRecent));
            var ownerId = SeedOwner(nameof(RecordVisit_CreatesVisit_AndSurfacesInRecent));

            var obj = Sample("INC-1", "Server down");
            CoreHub.ObjectManager.Add(obj);

            var visit = CoreHub.ObjectManager.RecordVisit(ownerId, obj.Id);

            Assert.NotNull(visit);

            var recent = CoreHub.ObjectManager.GetRecentObjects(ownerId, 10);
            Assert.Single(recent);
            Assert.Equal(obj.Id, recent[0].Id);
        }

        /// <summary>
        /// Verifies that visiting the same object twice updates the single visit row in place
        /// rather than creating a duplicate (the composite unique index contract).
        /// </summary>
        [Fact]
        public void RecordVisit_Twice_DoesNotDuplicate()
        {
            Seed(nameof(RecordVisit_Twice_DoesNotDuplicate));
            var ownerId = SeedOwner(nameof(RecordVisit_Twice_DoesNotDuplicate));

            var obj = Sample("INC-2", "Outage");
            CoreHub.ObjectManager.Add(obj);

            CoreHub.ObjectManager.RecordVisit(ownerId, obj.Id);
            CoreHub.ObjectManager.RecordVisit(ownerId, obj.Id);

            using var db = CoreHubFixture.CreateDbContext(nameof(RecordVisit_Twice_DoesNotDuplicate));
            Assert.Equal(1, db.ObjectVisits.Count(x => x.OwnerId == ownerId && x.ObjectId == obj.Id));
        }

        /// <summary>
        /// Verifies that <c>RecordVisit</c> with an unknown owner or object persists nothing and
        /// returns <c>null</c> (the foreign keys would otherwise reject the write).
        /// </summary>
        [Fact]
        public void RecordVisit_UnknownOwnerOrObject_ReturnsNull()
        {
            Seed(nameof(RecordVisit_UnknownOwnerOrObject_ReturnsNull));
            var ownerId = SeedOwner(nameof(RecordVisit_UnknownOwnerOrObject_ReturnsNull));

            var obj = Sample("INC-3", "Thing");
            CoreHub.ObjectManager.Add(obj);

            Assert.Null(CoreHub.ObjectManager.RecordVisit(Guid.NewGuid(), obj.Id));
            Assert.Null(CoreHub.ObjectManager.RecordVisit(ownerId, Guid.NewGuid()));
        }

        /// <summary>
        /// Verifies that <c>GetRecentObjects</c> orders by last-visited descending (newest first)
        /// and honours the count cap.
        /// </summary>
        [Fact]
        public void GetRecentObjects_OrdersByLastVisitedDescending()
        {
            Seed(nameof(GetRecentObjects_OrdersByLastVisitedDescending));
            var ownerId = SeedOwner(nameof(GetRecentObjects_OrdersByLastVisitedDescending));

            var older = Sample("INC-10", "older");
            var middle = Sample("INC-11", "middle");
            var newest = Sample("INC-12", "newest");
            CoreHub.ObjectManager.Add(older);
            CoreHub.ObjectManager.Add(middle);
            CoreHub.ObjectManager.Add(newest);

            var now = DateTime.UtcNow;
            SeedVisit(nameof(GetRecentObjects_OrdersByLastVisitedDescending), ownerId, older.Id, now.AddHours(-10));
            SeedVisit(nameof(GetRecentObjects_OrdersByLastVisitedDescending), ownerId, middle.Id, now.AddHours(-5));
            SeedVisit(nameof(GetRecentObjects_OrdersByLastVisitedDescending), ownerId, newest.Id, now.AddHours(-1));

            var recent = CoreHub.ObjectManager.GetRecentObjects(ownerId, 10);
            Assert.Equal([newest.Id, middle.Id, older.Id], recent.Select(o => o.Id).ToList());

            var capped = CoreHub.ObjectManager.GetRecentObjects(ownerId, 2);
            Assert.Equal([newest.Id, middle.Id], capped.Select(o => o.Id).ToList());
        }

        /// <summary>
        /// Verifies that <c>Add</c> derives the kind from the object's class — the
        /// class is the single source of the kind, so a caller-supplied kind is
        /// overruled by the class kind.
        /// </summary>
        [Fact]
        public void Add_DerivesKindFromClass()
        {
            Seed(nameof(Add_DerivesKindFromClass));
            var documentClassId = SeedClass(nameof(Add_DerivesKindFromClass), "Handbook", ObjectKind.Document);

            var document = Sample("DOC-1", "Belongs to a document class");
            document.ClassId = documentClassId;
            document.Kind = null;
            CoreHub.ObjectManager.Add(document);

            var overruled = Sample("INC-300", "Caller-supplied kind is overruled");
            overruled.Kind = "  Document ";
            CoreHub.ObjectManager.Add(overruled);

            Assert.Equal(ObjectKind.Document, CoreHub.ObjectManager.GetObject(document.Id)?.Kind);
            Assert.Equal(ObjectKind.Issue, CoreHub.ObjectManager.GetObject(overruled.Id)?.Kind);
        }

        /// <summary>
        /// Verifies that changing the kind of a class re-stamps the kind onto the
        /// existing objects of the class, so the kind overviews immediately reflect
        /// the change.
        /// </summary>
        [Fact]
        public void ClassKindChange_RestampsObjects()
        {
            Seed(nameof(ClassKindChange_RestampsObjects));

            var obj = Sample("INC-400", "Re-stamped on class change");
            CoreHub.ObjectManager.Add(obj);
            Assert.Equal(ObjectKind.Issue, CoreHub.ObjectManager.GetObject(obj.Id)?.Kind);

            var classEntity = CoreHub.ClassManager.GetClass(ClassId);
            Assert.NotNull(classEntity);
            classEntity.Kind = ObjectKind.Blog;
            CoreHub.ClassManager.Update(classEntity);

            Assert.Equal(ObjectKind.Blog, CoreHub.ObjectManager.GetObject(obj.Id)?.Kind);
        }

        /// <summary>
        /// Verifies the star round trip: <c>SetFavorite</c> flips the flag,
        /// <c>IsFavorite</c> reflects it, and <c>GetFavoriteObjects</c> surfaces only
        /// starred objects.
        /// </summary>
        [Fact]
        public void SetFavorite_Toggle_SurfacesInFavorites()
        {
            Seed(nameof(SetFavorite_Toggle_SurfacesInFavorites));
            var ownerId = SeedOwner(nameof(SetFavorite_Toggle_SurfacesInFavorites));

            var obj = Sample("INC-200", "Starrable");
            CoreHub.ObjectManager.Add(obj);

            Assert.False(CoreHub.ObjectManager.IsFavorite(ownerId, obj.Id));

            var visit = CoreHub.ObjectManager.SetFavorite(ownerId, obj.Id, true);
            Assert.NotNull(visit);
            Assert.True(CoreHub.ObjectManager.IsFavorite(ownerId, obj.Id));

            var favorites = CoreHub.ObjectManager.GetFavoriteObjects(ownerId);
            Assert.Single(favorites);
            Assert.Equal(obj.Id, favorites[0].Id);

            CoreHub.ObjectManager.SetFavorite(ownerId, obj.Id, false);
            Assert.False(CoreHub.ObjectManager.IsFavorite(ownerId, obj.Id));
            Assert.Empty(CoreHub.ObjectManager.GetFavoriteObjects(ownerId));
        }

        /// <summary>
        /// Verifies that starring alone does not surface the object in the recents (the
        /// star leaves the last-visited timestamp untouched) and that a subsequent visit
        /// keeps the star.
        /// </summary>
        [Fact]
        public void SetFavorite_DoesNotAffectRecents()
        {
            Seed(nameof(SetFavorite_DoesNotAffectRecents));
            var ownerId = SeedOwner(nameof(SetFavorite_DoesNotAffectRecents));

            var obj = Sample("INC-201", "Starred, never visited");
            CoreHub.ObjectManager.Add(obj);

            CoreHub.ObjectManager.SetFavorite(ownerId, obj.Id, true);
            Assert.Empty(CoreHub.ObjectManager.GetRecentObjects(ownerId, 10));

            CoreHub.ObjectManager.RecordVisit(ownerId, obj.Id);
            Assert.Single(CoreHub.ObjectManager.GetRecentObjects(ownerId, 10));
            Assert.True(CoreHub.ObjectManager.IsFavorite(ownerId, obj.Id));
        }

        /// <summary>
        /// Verifies that <c>SetFavorite</c> with an unknown owner or object persists
        /// nothing and returns <c>null</c>.
        /// </summary>
        [Fact]
        public void SetFavorite_UnknownOwnerOrObject_ReturnsNull()
        {
            Seed(nameof(SetFavorite_UnknownOwnerOrObject_ReturnsNull));
            var ownerId = SeedOwner(nameof(SetFavorite_UnknownOwnerOrObject_ReturnsNull));

            var obj = Sample("INC-202", "Thing");
            CoreHub.ObjectManager.Add(obj);

            Assert.Null(CoreHub.ObjectManager.SetFavorite(Guid.NewGuid(), obj.Id, true));
            Assert.Null(CoreHub.ObjectManager.SetFavorite(ownerId, Guid.NewGuid(), true));
        }

        /// <summary>
        /// Seeds an additional class with the supplied kind into the in-memory database
        /// and returns its id, so kind-derivation tests can attach objects to it.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="name">The name of the class.</param>
        /// <param name="kind">The object-kind key of the class.</param>
        /// <returns>The id of the seeded class.</returns>
        private static Guid SeedClass(string connectionString, string name, string kind)
        {
            var classId = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(connectionString);
            db.Classes.Add(new Class { Id = classId, Name = name, Kind = kind, WorkspaceId = WorkspaceId });
            db.SaveChanges();

            return classId;
        }

        /// <summary>
        /// Seeds an owning identity into the in-memory database and returns its id. The owner must
        /// exist so the visit foreign keys accept the write.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <returns>The id of the seeded identity.</returns>
        private static Guid SeedOwner(string connectionString)
        {
            var ownerId = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(connectionString);
            db.Identities.Add(new Identity
            {
                Id = ownerId,
                Name = "Owner",
                Email = "owner@kleenestar.org",
                PasswordHash = "$test$"
            });
            db.SaveChanges();

            return ownerId;
        }

        /// <summary>
        /// Seeds a single object visit with an explicit last-visited value so ordering tests do
        /// not depend on wall-clock resolution.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="ownerId">The owning identity id.</param>
        /// <param name="objectId">The visited object id.</param>
        /// <param name="lastVisited">The last-visited timestamp.</param>
        private static void SeedVisit(string connectionString, Guid ownerId, Guid objectId, DateTime lastVisited)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);
            db.ObjectVisits.Add(new ObjectVisit
            {
                OwnerId = ownerId,
                ObjectId = objectId,
                LastVisited = lastVisited,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates a sample object attached to the seeded class and workspace.
        /// </summary>
        /// <param name="key">The object key.</param>
        /// <param name="summary">The object summary.</param>
        /// <returns>The sample object.</returns>
        private static ObjectEntity Sample(string key, string summary) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Summary = summary,
            WorkspaceId = WorkspaceId,
            ClassId = ClassId
        };
    }
}
