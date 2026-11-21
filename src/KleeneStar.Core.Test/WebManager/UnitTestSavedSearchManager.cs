using KleeneStar.Core.Test;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.SavedSearchManager"/> —
    /// the per-identity saved-search CRUD plus the recency and starring helpers.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSavedSearchManager
    {
        private static readonly Guid OwnerId = Guid.Parse("BB223344-5566-7788-99AA-BBCCDDEEFF00");
        private static readonly Guid OtherOwnerId = Guid.Parse("CC334455-6677-8899-AABB-CCDDEEFF0011");

        /// <summary>
        /// Initializes the hub and seeds the owning identity.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Identities.Any(x => x.Id == OwnerId))
            {
                db.Identities.Add(new Identity
                {
                    Id = OwnerId,
                    Name = "Saved Search Owner",
                    Email = "owner@kleenestar.test",
                    PasswordHash = "$seed$v1$test"
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Builds an active saved search owned by <see cref="OwnerId"/>.
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <param name="starred">Whether the search is starred.</param>
        /// <param name="lastUsedHoursAgo">How long ago the search was last used.</param>
        /// <returns>The saved search.</returns>
        private static SavedSearch New(string name, bool starred, int lastUsedHoursAgo)
        {
            return new SavedSearch(Guid.NewGuid())
            {
                Name = name,
                Query = "Summary ~ \"x\"",
                OwnerId = OwnerId,
                Starred = starred,
                State = SavedSearchState.Active,
                LastUsed = DateTime.UtcNow.AddHours(-lastUsedHoursAgo)
            };
        }

        /// <summary>
        /// Verifies that an added saved search is returned by <c>GetForOwner</c> and not for a
        /// different owner.
        /// </summary>
        [Fact]
        public void Add_Then_GetForOwner_IsScopedToOwner()
        {
            Seed(nameof(Add_Then_GetForOwner_IsScopedToOwner));

            CoreHub.SavedSearchManager.Add(New("Alpha", starred: false, lastUsedHoursAgo: 1));

            Assert.Single(CoreHub.SavedSearchManager.GetForOwner(OwnerId));
            Assert.Empty(CoreHub.SavedSearchManager.GetForOwner(OtherOwnerId));
        }

        /// <summary>
        /// Verifies that <c>GetForOwner</c> orders starred searches before unstarred ones.
        /// </summary>
        [Fact]
        public void GetForOwner_OrdersStarredFirst()
        {
            Seed(nameof(GetForOwner_OrdersStarredFirst));

            CoreHub.SavedSearchManager.Add(New("Zebra", starred: false, lastUsedHoursAgo: 1));
            CoreHub.SavedSearchManager.Add(New("Apple", starred: true, lastUsedHoursAgo: 2));

            var result = CoreHub.SavedSearchManager.GetForOwner(OwnerId);

            Assert.Equal("Apple", result[0].Name);
            Assert.True(result[0].Starred);
            Assert.Equal("Zebra", result[1].Name);
        }

        /// <summary>
        /// Verifies that <c>GetRecent</c> orders by most-recently used first and honours the limit.
        /// </summary>
        [Fact]
        public void GetRecent_OrdersByLastUsedAndLimits()
        {
            Seed(nameof(GetRecent_OrdersByLastUsedAndLimits));

            CoreHub.SavedSearchManager.Add(New("Old", starred: false, lastUsedHoursAgo: 100));
            CoreHub.SavedSearchManager.Add(New("Fresh", starred: false, lastUsedHoursAgo: 1));

            var recent = CoreHub.SavedSearchManager.GetRecent(OwnerId, 1);

            Assert.Single(recent);
            Assert.Equal("Fresh", recent[0].Name);
        }

        /// <summary>
        /// Verifies that <c>SetStarred</c> persists the new starred flag.
        /// </summary>
        [Fact]
        public void SetStarred_TogglesFlag()
        {
            Seed(nameof(SetStarred_TogglesFlag));

            var item = New("Star me", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            CoreHub.SavedSearchManager.SetStarred(item.Id, true);

            Assert.True(CoreHub.SavedSearchManager.GetSavedSearch(item.Id).Starred);
        }

        /// <summary>
        /// Verifies that <c>SetColumns</c> stores the layout and that a blank one clears it.
        /// </summary>
        [Fact]
        public void SetColumns_StoresAndClearsTheLayout()
        {
            Seed(nameof(SetColumns_StoresAndClearsTheLayout));

            var item = New("Columns", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            CoreHub.SavedSearchManager.SetColumns(item.Id, "[{\"id\":\"summary\"}]");
            Assert.Equal("[{\"id\":\"summary\"}]", CoreHub.SavedSearchManager.GetSavedSearch(item.Id).Columns);

            CoreHub.SavedSearchManager.SetColumns(item.Id, " ");
            Assert.Null(CoreHub.SavedSearchManager.GetSavedSearch(item.Id).Columns);
        }

        /// <summary>
        /// Verifies that <c>RecordUse</c> advances the <c>LastUsed</c> timestamp.
        /// </summary>
        [Fact]
        public void RecordUse_AdvancesLastUsed()
        {
            Seed(nameof(RecordUse_AdvancesLastUsed));

            var item = New("Run me", starred: false, lastUsedHoursAgo: 48);
            CoreHub.SavedSearchManager.Add(item);
            var before = CoreHub.SavedSearchManager.GetSavedSearch(item.Id).LastUsed;

            CoreHub.SavedSearchManager.RecordUse(item.Id);

            var after = CoreHub.SavedSearchManager.GetSavedSearch(item.Id).LastUsed;
            Assert.True(after > before);
        }

        /// <summary>
        /// Verifies that removing a saved search soft-deletes it: it stops being surfaced by
        /// <c>GetForOwner</c> while the row is retained with state <c>Deleted</c>.
        /// </summary>
        [Fact]
        public void Remove_SoftDeletesSavedSearch()
        {
            Seed(nameof(Remove_SoftDeletesSavedSearch));

            var item = New("Delete me", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            CoreHub.SavedSearchManager.Remove(item.Id);

            Assert.Empty(CoreHub.SavedSearchManager.GetForOwner(OwnerId));

            var soft = CoreHub.SavedSearchManager.GetSavedSearch(item.Id);
            Assert.NotNull(soft);
            Assert.Equal(SavedSearchState.Deleted, soft.State);
        }

        /// <summary>
        /// Verifies that the owner holds every saved-search permission on their own search,
        /// without any grant.
        /// </summary>
        [Fact]
        public void IsGranted_OwnerMayDoEverything()
        {
            Seed(nameof(IsGranted_OwnerMayDoEverything));

            var item = New("Mine", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            Assert.True(CoreHub.SavedSearchManager.IsGranted(item, OwnerId, typeof(SavedSearchReadPermission)));
            Assert.True(CoreHub.SavedSearchManager.IsGranted(item, OwnerId, typeof(SavedSearchUpdatePermission)));
            Assert.True(CoreHub.SavedSearchManager.IsGranted(item, OwnerId, typeof(SavedSearchDeletePermission)));
            Assert.True(CoreHub.SavedSearchManager.IsGranted(item, OwnerId, typeof(SavedSearchManageProfilesPermission)));
        }

        /// <summary>
        /// Verifies that a saved search nobody shared is private: another identity is refused
        /// even reading it - the opposite of an unadministered workspace, which is open.
        /// </summary>
        [Fact]
        public void IsGranted_UnsharedSearchIsPrivate()
        {
            Seed(nameof(IsGranted_UnsharedSearchIsPrivate));

            var item = New("Private", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            Assert.False(CoreHub.SavedSearchManager.IsGranted(item, OtherOwnerId, typeof(SavedSearchReadPermission)));
            Assert.False(CoreHub.SavedSearchManager.IsGranted(item, OtherOwnerId, typeof(SavedSearchUpdatePermission)));
            Assert.Empty(CoreHub.SavedSearchManager.GetSharedWith(OtherOwnerId));
        }

        /// <summary>
        /// Verifies that nobody (<see cref="Guid.Empty"/>) and a deleted saved search are
        /// refused, the owner included.
        /// </summary>
        [Fact]
        public void IsGranted_RefusesNobodyAndDeletedSearches()
        {
            Seed(nameof(IsGranted_RefusesNobodyAndDeletedSearches));

            var item = New("Gone", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            Assert.False(CoreHub.SavedSearchManager.IsGranted(item, Guid.Empty, typeof(SavedSearchReadPermission)));

            CoreHub.SavedSearchManager.Remove(item.Id);
            var deleted = CoreHub.SavedSearchManager.GetSavedSearch(item.Id);

            Assert.False(CoreHub.SavedSearchManager.IsGranted(deleted, OwnerId, typeof(SavedSearchReadPermission)));
        }

        /// <summary>
        /// Verifies that a grant whose policy carries no saved-search permission shares nothing:
        /// the search stays out of the other identity's shared list, and the owner never finds
        /// their own search there.
        /// </summary>
        [Fact]
        public void GetSharedWith_IgnoresGrantsThatCarryNothing()
        {
            Seed(nameof(GetSharedWith_IgnoresGrantsThatCarryNothing));

            var item = New("Granted", starred: false, lastUsedHoursAgo: 1);
            CoreHub.SavedSearchManager.Add(item);

            // written directly: Assign refuses a policy the fixture's empty registry does not know
            ModelHub.Add(new PermissionAssignment(Guid.NewGuid())
            {
                Scope = PermissionScope.SavedSearch,
                ScopeId = item.Id.ToString(),
                GroupId = Group.AuthenticatedId,
                Policy = "savedsearch_view_policy",
                Created = DateTime.UtcNow
            });

            Assert.Empty(CoreHub.SavedSearchManager.GetSharedWith(OwnerId));
            Assert.DoesNotContain(item.Id, CoreHub.PermissionManager.GetGrantedIds(PermissionScope.SavedSearch, Guid.Empty, typeof(SavedSearchReadPermission)));
        }
    }
}
