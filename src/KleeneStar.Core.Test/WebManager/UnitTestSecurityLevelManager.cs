using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebIndex.Queries;

using ObjectEntity = KleeneStar.Model.Entities.Object;
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.SecurityLevelManager"/> and
    /// for the classification filter it puts on every object read.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSecurityLevelManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("6C1B7A2E-9D3F-4A81-9C7E-1F2A3B4C5D6E");
        private static readonly Guid ClassId = Guid.Parse("7D2C8B3F-AE40-4B92-8D6F-2A3B4C5D6E7F");
        private static readonly Guid ClearedGroupId = Guid.Parse("8E3D9C40-BF51-4CA3-9E70-3B4C5D6E7F80");
        private static readonly Guid OtherGroupId = Guid.Parse("9F4EAD51-C062-4DB4-AF81-4C5D6E7F8091");

        /// <summary>
        /// The identity the tests act as. The manager narrows what it answers to the
        /// signed-in user of the request; a test reaches it without a request and says who is
        /// acting through <c>ISessionManager.BeginIdentity</c>.
        /// </summary>
        private static readonly Guid CurrentIdentityId = Guid.Parse("77087646-B13A-44B1-9BAC-6E66443CEDFD");

        /// <summary>
        /// Seeds the workspace and class the levels belong to, plus two groups and the
        /// identity the ambient request is attributed to. The identity is a member of the
        /// cleared group and not of the other one.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <returns>The acting scope; closing it stops acting as that identity.</returns>
        private static IDisposable Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-sec", Name = "main" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            if (!db.Groups.Any(x => x.Id == ClearedGroupId))
            {
                var cleared = new Group { Id = ClearedGroupId, Name = "Cleared" };
                var other = new Group { Id = OtherGroupId, Name = "Other" };

                db.Groups.Add(cleared);
                db.Groups.Add(other);

                db.Identities.Add(new Identity
                {
                    Id = CurrentIdentityId,
                    Name = "Admin User",
                    UserName = "admin",
                    Email = "admin@example.test",
                    PasswordHash = "$test$",
                    GroupMemberships = [new IdentityGroupMembership { Group = cleared }]
                });
            }

            db.SaveChanges();

            return CoreHub.SessionManager.BeginIdentity(CurrentIdentityId);
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the level and that <c>GetSecurityLevel</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetSecurityLevel_RoundTrip()
        {
            using var acting = Seed(nameof(Add_Then_GetSecurityLevel_RoundTrip));

            var level = Sample("Confidential", ClearedGroupId);
            CoreHub.SecurityLevelManager.Add(level);

            var loaded = CoreHub.SecurityLevelManager.GetSecurityLevel(level.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Confidential", loaded.Name);
            Assert.Equal([ClearedGroupId], loaded.PermittedGroupIds);
        }

        /// <summary>
        /// Verifies that the levels of a class come back in rank order.
        /// </summary>
        [Fact]
        public void GetSecurityLevels_ByClassId_IsOrderedByRank()
        {
            using var acting = Seed(nameof(GetSecurityLevels_ByClassId_IsOrderedByRank));

            CoreHub.SecurityLevelManager.Add(Sample("Confidential", ClearedGroupId, rank: 20));
            CoreHub.SecurityLevelManager.Add(Sample("Public", ClearedGroupId, rank: 0));

            var result = CoreHub.SecurityLevelManager.GetSecurityLevels(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(["Public", "Confidential"], result.Select(x => x.Name));
        }

        /// <summary>
        /// Verifies the rule: an unclassified object is answered to everyone, a level naming
        /// a group the identity belongs to clears it, and one naming another group does not.
        /// </summary>
        [Fact]
        public void IsCleared_FollowsTheGroupsTheLevelNames()
        {
            using var acting = Seed(nameof(IsCleared_FollowsTheGroupsTheLevelNames));

            var mine = Sample("Mine", ClearedGroupId);
            var theirs = Sample("Theirs", OtherGroupId);

            CoreHub.SecurityLevelManager.Add(mine);
            CoreHub.SecurityLevelManager.Add(theirs);

            Assert.True(CoreHub.SecurityLevelManager.IsCleared(CurrentIdentityId, null));
            Assert.True(CoreHub.SecurityLevelManager.IsCleared(CurrentIdentityId, mine.Id));
            Assert.False(CoreHub.SecurityLevelManager.IsCleared(CurrentIdentityId, theirs.Id));
        }

        /// <summary>
        /// Verifies that a level naming no group at all is closed rather than unrestricted -
        /// the reading that separates a security level from a permission grant.
        /// </summary>
        [Fact]
        public void IsCleared_LevelWithoutGroups_IsClosed()
        {
            using var acting = Seed(nameof(IsCleared_LevelWithoutGroups_IsClosed));

            var closed = Sample("Closed");
            CoreHub.SecurityLevelManager.Add(closed);

            Assert.False(CoreHub.SecurityLevelManager.IsCleared(CurrentIdentityId, closed.Id));
        }

        /// <summary>
        /// Verifies that only the levels the identity is cleared for are offered for
        /// assignment, and that an archived one is never offered.
        /// </summary>
        [Fact]
        public void GetAssignableSecurityLevels_OffersOnlyClearedActiveLevels()
        {
            using var acting = Seed(nameof(GetAssignableSecurityLevels_OffersOnlyClearedActiveLevels));

            var mine = Sample("Mine", ClearedGroupId);
            var theirs = Sample("Theirs", OtherGroupId);
            var archived = Sample("Archived", ClearedGroupId);
            archived.State = SecurityLevelState.Archived;

            CoreHub.SecurityLevelManager.Add(mine);
            CoreHub.SecurityLevelManager.Add(theirs);
            CoreHub.SecurityLevelManager.Add(archived);

            var result = CoreHub.SecurityLevelManager.GetAssignableSecurityLevels(ClassId, CurrentIdentityId);

            Assert.Equal(["Mine"], result.Select(x => x.Name));
        }

        /// <summary>
        /// Verifies that a class starts its objects on exactly one level: marking a second
        /// one as the default demotes the first.
        /// </summary>
        [Fact]
        public void Add_SecondDefault_DemotesTheFirst()
        {
            using var acting = Seed(nameof(Add_SecondDefault_DemotesTheFirst));

            var first = Sample("First", ClearedGroupId);
            first.IsDefault = true;
            CoreHub.SecurityLevelManager.Add(first);

            var second = Sample("Second", ClearedGroupId);
            second.IsDefault = true;
            CoreHub.SecurityLevelManager.Add(second);

            Assert.False(CoreHub.SecurityLevelManager.GetSecurityLevel(first.Id).IsDefault);
            Assert.Equal(second.Id, CoreHub.SecurityLevelManager.GetDefaultSecurityLevel(ClassId)?.Id);
        }

        /// <summary>
        /// Verifies the central guarantee: an object classified with a level the caller is not
        /// cleared for is absent from every read the object manager answers, while the
        /// unclassified and the cleared ones come back.
        /// </summary>
        [Fact]
        public void ObjectManager_HidesObjectsTheCallerIsNotClearedFor()
        {
            using var acting = Seed(nameof(ObjectManager_HidesObjectsTheCallerIsNotClearedFor));

            var mine = Sample("Mine", ClearedGroupId);
            var theirs = Sample("Theirs", OtherGroupId);

            CoreHub.SecurityLevelManager.Add(mine);
            CoreHub.SecurityLevelManager.Add(theirs);

            var open = SampleObject("SEC-1", null);
            var cleared = SampleObject("SEC-2", mine.Id);
            var hidden = SampleObject("SEC-3", theirs.Id);

            CoreHub.ObjectManager.Add(open);
            CoreHub.ObjectManager.Add(cleared);
            CoreHub.ObjectManager.Add(hidden);

            var visible = CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()).ToList();

            Assert.Equal(["SEC-1", "SEC-2"], visible.Select(x => x.Key).OrderBy(x => x));
            Assert.Equal(2, CoreHub.ObjectManager.CountObjects(new Query<ObjectEntity>()));
            Assert.Null(CoreHub.ObjectManager.GetObject(hidden.Id));
            Assert.Null(CoreHub.ObjectManager.GetObjectByKey("SEC-3"));
            Assert.NotNull(CoreHub.ObjectManager.GetObjectByKey("SEC-2"));
        }

        /// <summary>
        /// Verifies that an unrestricted scope lifts the filter and that closing it puts it
        /// back - the escape hatch the system's own reads use.
        /// </summary>
        [Fact]
        public void BeginUnrestricted_LiftsTheFilterForTheScopeOnly()
        {
            using var acting = Seed(nameof(BeginUnrestricted_LiftsTheFilterForTheScopeOnly));

            var theirs = Sample("Theirs", OtherGroupId);
            CoreHub.SecurityLevelManager.Add(theirs);
            CoreHub.ObjectManager.Add(SampleObject("SEC-9", theirs.Id));

            Assert.Null(CoreHub.ObjectManager.GetObjectByKey("SEC-9"));

            using (CoreHub.SecurityLevelManager.BeginUnrestricted())
            {
                Assert.NotNull(CoreHub.ObjectManager.GetObjectByKey("SEC-9"));

                // scopes nest; closing the inner one must not restore the filter
                using (CoreHub.SecurityLevelManager.BeginUnrestricted())
                {
                    Assert.True(CoreHub.SecurityLevelManager.IsUnrestricted);
                }

                Assert.NotNull(CoreHub.ObjectManager.GetObjectByKey("SEC-9"));
            }

            Assert.False(CoreHub.SecurityLevelManager.IsUnrestricted);
            Assert.Null(CoreHub.ObjectManager.GetObjectByKey("SEC-9"));
        }

        /// <summary>
        /// Verifies that removing a level declassifies the objects that carried it rather
        /// than leaving them pointing at a level that is gone - which the visibility check
        /// would have to read as "cleared for nobody".
        /// </summary>
        [Fact]
        public void Remove_DeclassifiesTheObjectsItGuarded()
        {
            using var acting = Seed(nameof(Remove_DeclassifiesTheObjectsItGuarded));

            var theirs = Sample("Theirs", OtherGroupId);
            CoreHub.SecurityLevelManager.Add(theirs);

            var guarded = SampleObject("SEC-7", theirs.Id);
            CoreHub.ObjectManager.Add(guarded);

            Assert.Null(CoreHub.ObjectManager.GetObjectByKey("SEC-7"));

            CoreHub.SecurityLevelManager.Remove(theirs.Id);

            var loaded = CoreHub.ObjectManager.GetObjectByKey("SEC-7");

            Assert.NotNull(loaded);
            Assert.Null(loaded.SecurityLevelId);
        }

        /// <summary>
        /// Creates a sample security level on the seeded class.
        /// </summary>
        /// <param name="name">The level name.</param>
        /// <param name="groups">The groups the level clears.</param>
        /// <param name="rank">The rank of the level.</param>
        /// <returns>The sample level.</returns>
        private static SecurityLevelEntity Sample(string name, Guid? groups = null, int rank = 0) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClassId = ClassId,
            Rank = rank,
            State = SecurityLevelState.Active,
            PermittedGroupIds = groups.HasValue ? [groups.Value] : []
        };

        /// <summary>
        /// Creates a sample object of the seeded class.
        /// </summary>
        /// <param name="key">The object key.</param>
        /// <param name="securityLevelId">The level the object carries, or null.</param>
        /// <returns>The sample object.</returns>
        private static ObjectEntity SampleObject(string key, Guid? securityLevelId) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Summary = key,
            WorkspaceId = WorkspaceId,
            ClassId = ClassId,
            State = WorkspaceState.Active,
            SecurityLevelId = securityLevelId
        };
    }
}
