using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.CustomQuickfilterManager"/> —
    /// the CRUD of the quickfilters a user defines and, above all, which of them a given identity
    /// is offered in the bar of a given view.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCustomQuickfilterManager
    {
        private static readonly Guid OwnerId = Guid.Parse("1A2B3C4D-5E6F-4071-8192-A3B4C5D6E7F8");
        private static readonly Guid OtherOwnerId = Guid.Parse("2B3C4D5E-6F70-4182-93A4-B5C6D7E8F901");

        /// <summary>
        /// Initializes the hub and seeds the owning identities.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            foreach (var (id, name) in new[] { (OwnerId, "Filter Owner"), (OtherOwnerId, "Other Owner") })
            {
                if (!db.Identities.Any(x => x.Id == id))
                {
                    db.Identities.Add(new Identity
                    {
                        Id = id,
                        Name = name,
                        Email = $"{id}@kleenestar.test",
                        PasswordHash = "$seed$v1$test"
                    });
                }
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Builds a quickfilter for the given view.
        /// </summary>
        /// <param name="name">The chip label.</param>
        /// <param name="viewKey">The view the filter belongs to.</param>
        /// <param name="contextKey">The context narrowing the view, or null.</param>
        /// <param name="ownerId">The identity that defined the filter.</param>
        /// <param name="shared">Whether the filter is offered to everyone.</param>
        /// <returns>The quickfilter.</returns>
        private static CustomQuickfilter New(string name, string viewKey, string contextKey, Guid ownerId, bool shared)
        {
            var now = DateTime.UtcNow;

            return new CustomQuickfilter(Guid.NewGuid())
            {
                Name = name,
                ViewKey = viewKey,
                ContextKey = contextKey,
                Query = "Name ~ \"a\"",
                OwnerId = ownerId,
                Shared = shared,
                Created = now,
                Updated = now
            };
        }

        /// <summary>
        /// Verifies that a personal filter is offered to its owner and withheld from everyone else.
        /// </summary>
        [Fact]
        public void GetVisible_PersonalFilter_IsOfferedToItsOwnerOnly()
        {
            Seed(nameof(GetVisible_PersonalFilter_IsOfferedToItsOwnerOnly));

            CoreHub.CustomQuickfilterManager.Add(New("Mine", "tenants", null, OwnerId, shared: false));

            Assert.Single(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("tenants", null, OwnerId));
            Assert.Empty(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("tenants", null, OtherOwnerId));
        }

        /// <summary>
        /// Verifies that a shared filter reaches an identity that did not define it.
        /// </summary>
        [Fact]
        public void GetVisible_SharedFilter_IsOfferedToEveryone()
        {
            Seed(nameof(GetVisible_SharedFilter_IsOfferedToEveryone));

            CoreHub.CustomQuickfilterManager.Add(New("Ours", "tenants", null, OwnerId, shared: true));

            Assert.Single(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("tenants", null, OtherOwnerId));
        }

        /// <summary>
        /// Verifies that a filter stays in the bar of the view it was defined in.
        /// </summary>
        [Fact]
        public void GetVisible_IsScopedToTheView()
        {
            Seed(nameof(GetVisible_IsScopedToTheView));

            CoreHub.CustomQuickfilterManager.Add(New("Mine", "tenants", null, OwnerId, shared: false));

            Assert.Empty(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("workspaces", null, OwnerId));
        }

        /// <summary>
        /// Verifies that a filter of one workspace is not offered in another, while a filter without
        /// a context stays out of both.
        /// </summary>
        [Fact]
        public void GetVisible_IsScopedToTheContext()
        {
            Seed(nameof(GetVisible_IsScopedToTheContext));

            CoreHub.CustomQuickfilterManager.Add(New("Here", "issues", "ws-1", OwnerId, shared: false));
            CoreHub.CustomQuickfilterManager.Add(New("Global", "issues", null, OwnerId, shared: false));

            Assert.Single(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("issues", "ws-1", OwnerId));
            Assert.Empty(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("issues", "ws-2", OwnerId));
            Assert.Single(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("issues", null, OwnerId));
        }

        /// <summary>
        /// Verifies the ordering the bar relies on: by ordinal, then by name.
        /// </summary>
        [Fact]
        public void GetVisible_OrdersByOrdinalThenName()
        {
            Seed(nameof(GetVisible_OrdersByOrdinalThenName));

            var second = New("Zulu", "tenants", null, OwnerId, shared: false);
            second.Ordinal = 0;
            var third = New("Alpha", "tenants", null, OwnerId, shared: false);
            third.Ordinal = 1;
            var first = New("Alpha", "tenants", null, OwnerId, shared: false);
            first.Ordinal = 0;

            CoreHub.CustomQuickfilterManager.Add(third);
            CoreHub.CustomQuickfilterManager.Add(second);
            CoreHub.CustomQuickfilterManager.Add(first);

            var names = CoreHub.CustomQuickfilterManager
                .GetVisibleCustomQuickfilters("tenants", null, OwnerId)
                .Select(x => $"{x.Ordinal}:{x.Name}")
                .ToArray();

            Assert.Equal(["0:Alpha", "0:Zulu", "1:Alpha"], names);
        }

        /// <summary>
        /// Verifies that a removed filter disappears from the bar.
        /// </summary>
        [Fact]
        public void Remove_TakesTheFilterOutOfTheBar()
        {
            Seed(nameof(Remove_TakesTheFilterOutOfTheBar));

            var filter = New("Mine", "tenants", null, OwnerId, shared: false);
            CoreHub.CustomQuickfilterManager.Add(filter);

            CoreHub.CustomQuickfilterManager.Remove(filter.Id);

            Assert.Empty(CoreHub.CustomQuickfilterManager.GetVisibleCustomQuickfilters("tenants", null, OwnerId));
            Assert.Null(CoreHub.CustomQuickfilterManager.GetCustomQuickfilter(filter.Id));
        }

        /// <summary>
        /// Verifies that the chip id a filter is offered under parses back to the filter.
        /// </summary>
        [Fact]
        public void FilterId_RoundTrips()
        {
            var filter = New("Mine", "tenants", null, OwnerId, shared: false);

            Assert.Equal(filter.Id, CustomQuickfilter.ParseFilterId(filter.FilterId));
        }

        /// <summary>
        /// Verifies that a chip belonging to a view rather than to a stored filter is not mistaken
        /// for one, so the view keeps interpreting its own chips.
        /// </summary>
        [Theory]
        [InlineData("qf_active")]
        [InlineData("qf_custom_")]
        [InlineData("qf_custom_not-a-guid")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseFilterId_IgnoresWhatIsNotAStoredFilter(string filterId)
        {
            Assert.Null(CustomQuickfilter.ParseFilterId(filterId));
        }
    }
}
