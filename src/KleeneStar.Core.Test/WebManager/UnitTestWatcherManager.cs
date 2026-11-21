using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.WatcherManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWatcherManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C5FF75F8-B051-4F55-EC09-DF728E8FCF55");
        private static readonly Guid ClassId = Guid.Parse("D6FF86F9-C162-4066-FD1A-E0839F9FD066");
        private static readonly Guid ObjectId = Guid.Parse("E7FF97FA-D273-4177-0E2B-F1940A0AE177");
        private static readonly Guid OtherObjectId = Guid.Parse("F8FFA8FB-E384-4288-1F3C-02A51B1BF288");
        private static readonly Guid IdentityId = Guid.Parse("A9FFB9FC-F495-4399-203D-13B62C2C0399");
        private static readonly Guid OtherIdentityId = Guid.Parse("BAFFCAFD-05A6-44AA-314E-24C73D3D14AA");

        /// <summary>
        /// Seeds the in-memory database with two objects and two identities so each
        /// test can persist and query watch relationships without relying on data
        /// produced by sibling tests.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wm", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Identities.Any(x => x.Id == IdentityId))
            {
                db.Identities.Add(new Identity { Id = IdentityId, Name = "Watcher One", Email = "w1@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Identities.Any(x => x.Id == OtherIdentityId))
            {
                db.Identities.Add(new Identity { Id = OtherIdentityId, Name = "Watcher Two", Email = "w2@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = ObjectId, Key = "WM-100", Summary = "watched item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }
            if (!db.Objects.Any(x => x.Id == OtherObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = OtherObjectId, Key = "WM-101", Summary = "unwatched item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add → GetWatchers round-trip: a single watch is persisted and visible on
        /// the object, with the related <see cref="Identity"/> hydrated.
        /// </summary>
        [Fact]
        public void Add_Then_GetWatchers_RoundTrip()
        {
            Seed(nameof(Add_Then_GetWatchers_RoundTrip));

            var watcher = CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            Assert.NotNull(watcher);
            Assert.Equal(ObjectId, watcher.ObjectId);
            Assert.Equal(IdentityId, watcher.IdentityId);

            var loaded = CoreHub.WatcherManager.GetWatchers(ObjectId).ToList();
            Assert.Single(loaded);
            Assert.Equal(IdentityId, loaded[0].IdentityId);
            Assert.NotNull(loaded[0].Identity);
            Assert.Equal("Watcher One", loaded[0].Identity.Name);
        }

        /// <summary>
        /// A second Add for the same (object, identity) pair must NOT create a second
        /// row — the unique composite index makes this contract a hard constraint and
        /// the manager normalises it into a silent return of the existing row.
        /// </summary>
        [Fact]
        public void Add_Duplicate_IsIdempotent()
        {
            Seed(nameof(Add_Duplicate_IsIdempotent));

            var first = CoreHub.WatcherManager.Add(ObjectId, IdentityId);
            var second = CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Single(CoreHub.WatcherManager.GetWatchers(ObjectId));
        }

        /// <summary>
        /// Add with an unknown object or unknown identity returns <c>null</c> and
        /// persists nothing. The endpoint translates this into a <c>404</c> response.
        /// </summary>
        [Fact]
        public void Add_UnknownObjectOrIdentity_ReturnsNull()
        {
            Seed(nameof(Add_UnknownObjectOrIdentity_ReturnsNull));

            var withUnknownObject = CoreHub.WatcherManager.Add(Guid.NewGuid(), IdentityId);
            var withUnknownIdentity = CoreHub.WatcherManager.Add(ObjectId, Guid.NewGuid());

            Assert.Null(withUnknownObject);
            Assert.Null(withUnknownIdentity);
            Assert.Empty(CoreHub.WatcherManager.GetWatchers(ObjectId));
        }

        /// <summary>
        /// Multiple identities can watch the same object independently and each shows
        /// up in <see cref="IWatcherManager.GetWatchers(Guid)"/>.
        /// </summary>
        [Fact]
        public void Add_MultipleIdentities_AllReturned()
        {
            Seed(nameof(Add_MultipleIdentities_AllReturned));

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);
            CoreHub.WatcherManager.Add(ObjectId, OtherIdentityId);

            var watchers = CoreHub.WatcherManager.GetWatchers(ObjectId).ToList();

            Assert.Equal(2, watchers.Count);
            Assert.Contains(watchers, w => w.IdentityId == IdentityId);
            Assert.Contains(watchers, w => w.IdentityId == OtherIdentityId);
        }

        /// <summary>
        /// A watch added to one object must not bleed into the watchers of another
        /// object. The query filter on <c>ObjectId</c> is the only thing keeping
        /// per-object isolation, so this is worth pinning down.
        /// </summary>
        [Fact]
        public void GetWatchers_OtherObject_ReturnsEmpty()
        {
            Seed(nameof(GetWatchers_OtherObject_ReturnsEmpty));

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            Assert.Empty(CoreHub.WatcherManager.GetWatchers(OtherObjectId));
        }

        /// <summary>
        /// <see cref="IWatcherManager.GetWatchers(ObjectKeyParameter)"/> resolves the
        /// object via its <see cref="ObjectEntity.Key"/> and returns the same set as
        /// the id-based overload.
        /// </summary>
        [Fact]
        public void GetWatchers_ByObjectKeyParameter_ResolvesByKey()
        {
            Seed(nameof(GetWatchers_ByObjectKeyParameter_ResolvesByKey));

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            var loaded = CoreHub.WatcherManager.GetWatchers(new ObjectKeyParameter("WM-100")).ToList();

            Assert.Single(loaded);
            Assert.Equal(IdentityId, loaded[0].IdentityId);
        }

        /// <summary>
        /// <see cref="IWatcherManager.GetWatchers(ObjectKeyParameter)"/> with an
        /// unknown key short-circuits to an empty collection instead of throwing.
        /// </summary>
        [Fact]
        public void GetWatchers_ByObjectKeyParameter_UnknownKey_ReturnsEmpty()
        {
            Seed(nameof(GetWatchers_ByObjectKeyParameter_UnknownKey_ReturnsEmpty));

            var loaded = CoreHub.WatcherManager.GetWatchers(new ObjectKeyParameter("does-not-exist"));

            Assert.Empty(loaded);
        }

        /// <summary>
        /// Remove deletes the watch row and raises
        /// <see cref="IWatcherManager.WatcherRemoved"/>; the second Remove call is a
        /// no-op and returns <c>false</c>.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            ObjectWatcher raised = null;
            CoreHub.WatcherManager.WatcherRemoved += (_, w) => raised = w;

            var first = CoreHub.WatcherManager.Remove(ObjectId, IdentityId);
            var second = CoreHub.WatcherManager.Remove(ObjectId, IdentityId);

            Assert.True(first);
            Assert.False(second);
            Assert.NotNull(raised);
            Assert.Equal(IdentityId, raised.IdentityId);
            Assert.Empty(CoreHub.WatcherManager.GetWatchers(ObjectId));
        }

        /// <summary>
        /// Remove with an unknown pair returns <c>false</c> and leaves existing
        /// watches alone.
        /// </summary>
        [Fact]
        public void Remove_Unknown_IsNoOp()
        {
            Seed(nameof(Remove_Unknown_IsNoOp));

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            var removed = CoreHub.WatcherManager.Remove(ObjectId, OtherIdentityId);

            Assert.False(removed);
            Assert.Single(CoreHub.WatcherManager.GetWatchers(ObjectId));
        }

        /// <summary>
        /// Add raises <see cref="IWatcherManager.WatcherAdded"/> exactly once on the
        /// first call and is silent on the idempotent second call.
        /// </summary>
        [Fact]
        public void Add_RaisesEvent_OnlyOnFirstInsert()
        {
            Seed(nameof(Add_RaisesEvent_OnlyOnFirstInsert));

            var raiseCount = 0;
            CoreHub.WatcherManager.WatcherAdded += (_, _) => raiseCount++;

            CoreHub.WatcherManager.Add(ObjectId, IdentityId);
            CoreHub.WatcherManager.Add(ObjectId, IdentityId);

            Assert.Equal(1, raiseCount);
        }
    }
}
