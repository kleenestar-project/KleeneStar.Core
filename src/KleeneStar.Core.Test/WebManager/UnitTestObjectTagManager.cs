using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectTagManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectTagManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C51175F8-B051-4F55-EC09-DF728E8FCF11");
        private static readonly Guid ClassId = Guid.Parse("D61286F9-C162-4066-FD1A-E0839F9FD012");
        private static readonly Guid ObjectId = Guid.Parse("E71397FA-D273-4177-0E2B-F1940A0AE013");
        private static readonly Guid OtherObjectId = Guid.Parse("F814A8FB-E384-4288-1F3C-02A51B1BF014");

        /// <summary>
        /// Seeds the in-memory database with two objects so each test can attach and
        /// query tags without relying on data produced by sibling tests.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-tm", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = ObjectId, Key = "TM-100", Summary = "tagged item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }
            if (!db.Objects.Any(x => x.Id == OtherObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = OtherObjectId, Key = "TM-101", Summary = "untagged item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add → GetTags round-trip: a single tag is persisted with name and color and
        /// visible on the object.
        /// </summary>
        [Fact]
        public void Add_Then_GetTags_RoundTrip()
        {
            Seed(nameof(Add_Then_GetTags_RoundTrip));

            var tag = CoreHub.ObjectTagManager.Add(ObjectId, "network", "#0d6efd");

            Assert.NotNull(tag);
            Assert.Equal(ObjectId, tag.ObjectId);
            Assert.Equal("network", tag.Name);
            Assert.Equal("#0d6efd", tag.Color);

            var loaded = CoreHub.ObjectTagManager.GetTags(ObjectId).ToList();
            Assert.Single(loaded);
            Assert.Equal("network", loaded[0].Name);
        }

        /// <summary>
        /// Attaching a tag whose name already exists on the object returns the
        /// existing row instead of creating a duplicate — the composite unique index
        /// on (ObjectId, Name) makes this a hard constraint.
        /// </summary>
        [Fact]
        public void Add_DuplicateName_IsIdempotent()
        {
            Seed(nameof(Add_DuplicateName_IsIdempotent));

            var first = CoreHub.ObjectTagManager.Add(ObjectId, "network", "#0d6efd");
            var second = CoreHub.ObjectTagManager.Add(ObjectId, "network", "#ff0000");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first.Id, second.Id);
            Assert.Single(CoreHub.ObjectTagManager.GetTags(ObjectId));
        }

        /// <summary>
        /// Add with an unknown object or an empty name returns <c>null</c> and
        /// persists nothing.
        /// </summary>
        [Fact]
        public void Add_UnknownObjectOrEmptyName_ReturnsNull()
        {
            Seed(nameof(Add_UnknownObjectOrEmptyName_ReturnsNull));

            var withUnknownObject = CoreHub.ObjectTagManager.Add(Guid.NewGuid(), "network", null);
            var withEmptyName = CoreHub.ObjectTagManager.Add(ObjectId, " ", null);

            Assert.Null(withUnknownObject);
            Assert.Null(withEmptyName);
            Assert.Empty(CoreHub.ObjectTagManager.GetTags(ObjectId));
        }

        /// <summary>
        /// Tags attached to one object must not bleed into the tags of another object.
        /// </summary>
        [Fact]
        public void GetTags_OtherObject_ReturnsEmpty()
        {
            Seed(nameof(GetTags_OtherObject_ReturnsEmpty));

            CoreHub.ObjectTagManager.Add(ObjectId, "network", null);

            Assert.Empty(CoreHub.ObjectTagManager.GetTags(OtherObjectId));
        }

        /// <summary>
        /// <see cref="IObjectTagManager.GetTags(ObjectKeyParameter)"/> resolves the
        /// object via its <see cref="ObjectEntity.Key"/>; an unknown key
        /// short-circuits to an empty collection.
        /// </summary>
        [Fact]
        public void GetTags_ByObjectKeyParameter_ResolvesByKey()
        {
            Seed(nameof(GetTags_ByObjectKeyParameter_ResolvesByKey));

            CoreHub.ObjectTagManager.Add(ObjectId, "vpn", null);

            var loaded = CoreHub.ObjectTagManager.GetTags(new ObjectKeyParameter("TM-100")).ToList();
            Assert.Single(loaded);
            Assert.Equal("vpn", loaded[0].Name);

            Assert.Empty(CoreHub.ObjectTagManager.GetTags(new ObjectKeyParameter("does-not-exist")));
        }

        /// <summary>
        /// Remove deletes the tag row and raises
        /// <see cref="IObjectTagManager.TagRemoved"/>; removing an unknown tag id is a
        /// no-op returning <c>false</c>.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var tag = CoreHub.ObjectTagManager.Add(ObjectId, "network", null);
            Assert.NotNull(tag);

            ObjectTag raised = null;
            CoreHub.ObjectTagManager.TagRemoved += (_, t) => raised = t;

            var first = CoreHub.ObjectTagManager.Remove(tag.Id);
            var second = CoreHub.ObjectTagManager.Remove(tag.Id);

            Assert.True(first);
            Assert.False(second);
            Assert.NotNull(raised);
            Assert.Equal("network", raised.Name);
            Assert.Empty(CoreHub.ObjectTagManager.GetTags(ObjectId));
        }

        /// <summary>
        /// Add raises <see cref="IObjectTagManager.TagAdded"/> exactly once on the
        /// first call and is silent on the idempotent duplicate call.
        /// </summary>
        [Fact]
        public void Add_RaisesEvent_OnlyOnFirstInsert()
        {
            Seed(nameof(Add_RaisesEvent_OnlyOnFirstInsert));

            var raiseCount = 0;
            CoreHub.ObjectTagManager.TagAdded += (_, _) => raiseCount++;

            CoreHub.ObjectTagManager.Add(ObjectId, "network", null);
            CoreHub.ObjectTagManager.Add(ObjectId, "network", null);

            Assert.Equal(1, raiseCount);
        }
    }
}
