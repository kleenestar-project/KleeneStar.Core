using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ShareManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestShareManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C50175F8-B051-4F55-EC09-DF728E8FCF01");
        private static readonly Guid ClassId = Guid.Parse("D60286F9-C162-4066-FD1A-E0839F9FD002");
        private static readonly Guid ObjectId = Guid.Parse("E70397FA-D273-4177-0E2B-F1940A0AE003");
        private static readonly Guid OtherObjectId = Guid.Parse("F804A8FB-E384-4288-1F3C-02A51B1BF004");
        private static readonly Guid IdentityId = Guid.Parse("A905B9FC-F495-4399-203D-13B62C2C0005");
        private static readonly Guid OtherIdentityId = Guid.Parse("BA06CAFD-05A6-44AA-314E-24C73D3D1006");

        /// <summary>
        /// Seeds the in-memory database with two objects and two identities so each
        /// test can persist and query share relationships without relying on data
        /// produced by sibling tests.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-sm", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Identities.Any(x => x.Id == IdentityId))
            {
                db.Identities.Add(new Identity { Id = IdentityId, Name = "Share One", Email = "s1@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Identities.Any(x => x.Id == OtherIdentityId))
            {
                db.Identities.Add(new Identity { Id = OtherIdentityId, Name = "Share Two", Email = "s2@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = ObjectId, Key = "SM-100", Summary = "shared item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }
            if (!db.Objects.Any(x => x.Id == OtherObjectId))
            {
                db.Objects.Add(new ObjectEntity { Id = OtherObjectId, Key = "SM-101", Summary = "unshared item", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add → GetShares round-trip: a single share is persisted and visible on the
        /// object, with the related <see cref="Identity"/> hydrated.
        /// </summary>
        [Fact]
        public void Add_Then_GetShares_RoundTrip()
        {
            Seed(nameof(Add_Then_GetShares_RoundTrip));

            var share = CoreHub.ShareManager.Add(ObjectId, IdentityId);

            Assert.NotNull(share);
            Assert.Equal(ObjectId, share.ObjectId);
            Assert.Equal(IdentityId, share.IdentityId);

            var loaded = CoreHub.ShareManager.GetShares(ObjectId).ToList();
            Assert.Single(loaded);
            Assert.Equal(IdentityId, loaded[0].IdentityId);
            Assert.NotNull(loaded[0].Identity);
            Assert.Equal("Share One", loaded[0].Identity.Name);
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

            var first = CoreHub.ShareManager.Add(ObjectId, IdentityId);
            var second = CoreHub.ShareManager.Add(ObjectId, IdentityId);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Single(CoreHub.ShareManager.GetShares(ObjectId));
        }

        /// <summary>
        /// Add with an unknown object or unknown identity returns <c>null</c> and
        /// persists nothing.
        /// </summary>
        [Fact]
        public void Add_UnknownObjectOrIdentity_ReturnsNull()
        {
            Seed(nameof(Add_UnknownObjectOrIdentity_ReturnsNull));

            var withUnknownObject = CoreHub.ShareManager.Add(Guid.NewGuid(), IdentityId);
            var withUnknownIdentity = CoreHub.ShareManager.Add(ObjectId, Guid.NewGuid());

            Assert.Null(withUnknownObject);
            Assert.Null(withUnknownIdentity);
            Assert.Empty(CoreHub.ShareManager.GetShares(ObjectId));
        }

        /// <summary>
        /// Multiple identities can hold shares on the same object independently and
        /// each shows up in <see cref="IShareManager.GetShares(Guid)"/>.
        /// </summary>
        [Fact]
        public void Add_MultipleIdentities_AllReturned()
        {
            Seed(nameof(Add_MultipleIdentities_AllReturned));

            CoreHub.ShareManager.Add(ObjectId, IdentityId);
            CoreHub.ShareManager.Add(ObjectId, OtherIdentityId);

            var shares = CoreHub.ShareManager.GetShares(ObjectId).ToList();

            Assert.Equal(2, shares.Count);
            Assert.Contains(shares, s => s.IdentityId == IdentityId);
            Assert.Contains(shares, s => s.IdentityId == OtherIdentityId);
        }

        /// <summary>
        /// A share granted on one object must not bleed into the shares of another
        /// object.
        /// </summary>
        [Fact]
        public void GetShares_OtherObject_ReturnsEmpty()
        {
            Seed(nameof(GetShares_OtherObject_ReturnsEmpty));

            CoreHub.ShareManager.Add(ObjectId, IdentityId);

            Assert.Empty(CoreHub.ShareManager.GetShares(OtherObjectId));
        }

        /// <summary>
        /// <see cref="IShareManager.GetShares(ObjectKeyParameter)"/> resolves the
        /// object via its <see cref="ObjectEntity.Key"/> and returns the same set as
        /// the id-based overload; an unknown key short-circuits to an empty collection.
        /// </summary>
        [Fact]
        public void GetShares_ByObjectKeyParameter_ResolvesByKey()
        {
            Seed(nameof(GetShares_ByObjectKeyParameter_ResolvesByKey));

            CoreHub.ShareManager.Add(ObjectId, IdentityId);

            var loaded = CoreHub.ShareManager.GetShares(new ObjectKeyParameter("SM-100")).ToList();
            Assert.Single(loaded);
            Assert.Equal(IdentityId, loaded[0].IdentityId);

            Assert.Empty(CoreHub.ShareManager.GetShares(new ObjectKeyParameter("does-not-exist")));
        }

        /// <summary>
        /// Remove deletes the share row and raises
        /// <see cref="IShareManager.ShareRemoved"/>; the second Remove call is a no-op
        /// and returns <c>false</c>.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            CoreHub.ShareManager.Add(ObjectId, IdentityId);

            ObjectShare raised = null;
            CoreHub.ShareManager.ShareRemoved += (_, s) => raised = s;

            var first = CoreHub.ShareManager.Remove(ObjectId, IdentityId);
            var second = CoreHub.ShareManager.Remove(ObjectId, IdentityId);

            Assert.True(first);
            Assert.False(second);
            Assert.NotNull(raised);
            Assert.Equal(IdentityId, raised.IdentityId);
            Assert.Empty(CoreHub.ShareManager.GetShares(ObjectId));
        }

        /// <summary>
        /// Add raises <see cref="IShareManager.ShareAdded"/> exactly once on the first
        /// call and is silent on the idempotent second call.
        /// </summary>
        [Fact]
        public void Add_RaisesEvent_OnlyOnFirstInsert()
        {
            Seed(nameof(Add_RaisesEvent_OnlyOnFirstInsert));

            var raiseCount = 0;
            CoreHub.ShareManager.ShareAdded += (_, _) => raiseCount++;

            CoreHub.ShareManager.Add(ObjectId, IdentityId);
            CoreHub.ShareManager.Add(ObjectId, IdentityId);

            Assert.Equal(1, raiseCount);
        }
    }
}
