using KleeneStar.Core.Test;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebQuickfilter
{
    /// <summary>
    /// Provides unit tests for <see cref="CustomQuickfilterSupport"/> — the half that turns an
    /// active chip back into a condition on the query the view answers with.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCustomQuickfilterSupport
    {
        private static readonly Guid OwnerId = Guid.Parse("3C4D5E6F-7081-4293-A4B5-C6D7E8F90123");

        /// <summary>
        /// Initializes the hub and seeds the owning identity and the tenants the filters select
        /// from.
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
                    Name = "Filter Owner",
                    Email = "owner@kleenestar.test",
                    PasswordHash = "$seed$v1$test"
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Stores a quickfilter carrying the given expression and returns the chip id it is offered
        /// under.
        /// </summary>
        /// <param name="query">The WQL expression.</param>
        /// <param name="viewKey">The view the filter belongs to.</param>
        /// <returns>The chip id.</returns>
        private static string Store(string query, string viewKey = "tenants")
        {
            var now = DateTime.UtcNow;
            var filter = new CustomQuickfilter(Guid.NewGuid())
            {
                Name = "Stored",
                ViewKey = viewKey,
                Query = query,
                OwnerId = OwnerId,
                Created = now,
                Updated = now
            };

            CoreHub.CustomQuickfilterManager.Add(filter);

            return filter.FilterId;
        }

        /// <summary>
        /// Builds the tenants the filters are applied to.
        /// </summary>
        /// <returns>The tenants.</returns>
        private static IQueryable<Tenant> Tenants()
        {
            return new[]
            {
                new Tenant(Guid.NewGuid()) { Name = "Acme", State = TenantState.Active },
                new Tenant(Guid.NewGuid()) { Name = "Globex", State = TenantState.Active },
                new Tenant(Guid.NewGuid()) { Name = "Initech", State = TenantState.Archived }
            }.AsQueryable();
        }

        /// <summary>
        /// Verifies that a stored expression narrows the result.
        /// </summary>
        [Fact]
        public void Apply_StoredExpression_NarrowsTheResult()
        {
            Seed(nameof(Apply_StoredExpression_NarrowsTheResult));

            var filterId = Store("Name = \"Acme\"");
            var query = CustomQuickfilterSupport.Apply([filterId], new Query<Tenant>(), "tenants");

            var result = query.Apply(Tenants()).ToList();

            Assert.Single(result);
            Assert.Equal("Acme", result[0].Name);
        }

        /// <summary>
        /// Verifies that the stored expression is composed onto the running query rather than
        /// replacing it, so it combines with what the view already narrowed.
        /// </summary>
        [Fact]
        public void Apply_ComposesOntoTheRunningQuery()
        {
            Seed(nameof(Apply_ComposesOntoTheRunningQuery));

            var filterId = Store("Name = \"Initech\"");

            // the view's own chip already restricted the query to the active tenants; Initech is
            // archived, so composing must leave nothing rather than bringing it back
            var query = new Query<Tenant>().Where(x => x.State == TenantState.Active) as IQuery<Tenant>;
            query = CustomQuickfilterSupport.Apply([filterId], query, "tenants");

            Assert.Empty(query.Apply(Tenants()).ToList());
        }

        /// <summary>
        /// Verifies that a filter belonging to another view is ignored even when its chip id is
        /// passed in, because its expression names fields this type does not have.
        /// </summary>
        [Fact]
        public void Apply_IgnoresAFilterOfAnotherView()
        {
            Seed(nameof(Apply_IgnoresAFilterOfAnotherView));

            var filterId = Store("Name = \"Acme\"", viewKey: "workspaces");
            var query = CustomQuickfilterSupport.Apply([filterId], new Query<Tenant>(), "tenants");

            Assert.Equal(3, query.Apply(Tenants()).Count());
        }

        /// <summary>
        /// Verifies that the view's own chips travel through untouched, so the switch that handles
        /// them still sees them.
        /// </summary>
        [Fact]
        public void Apply_LeavesTheViewsOwnChipsAlone()
        {
            Seed(nameof(Apply_LeavesTheViewsOwnChipsAlone));

            var query = CustomQuickfilterSupport.Apply(["qf_active"], new Query<Tenant>(), "tenants");

            Assert.Equal(3, query.Apply(Tenants()).Count());
        }

        /// <summary>
        /// Verifies that an expression that no longer parses leaves the view usable instead of
        /// taking it down.
        /// </summary>
        [Fact]
        public void Apply_SkipsAnExpressionThatNoLongerParses()
        {
            Seed(nameof(Apply_SkipsAnExpressionThatNoLongerParses));

            var filterId = Store("=== not a query ===");
            var query = CustomQuickfilterSupport.Apply([filterId], new Query<Tenant>(), "tenants");

            Assert.Equal(3, query.Apply(Tenants()).Count());
        }

        /// <summary>
        /// Verifies that a chip whose filter was deleted meanwhile is ignored.
        /// </summary>
        [Fact]
        public void Apply_IgnoresAFilterThatIsGone()
        {
            Seed(nameof(Apply_IgnoresAFilterThatIsGone));

            var filterId = CustomQuickfilter.IdPrefix + Guid.NewGuid();
            var query = CustomQuickfilterSupport.Apply([filterId], new Query<Tenant>(), "tenants");

            Assert.Equal(3, query.Apply(Tenants()).Count());
        }

        /// <summary>
        /// Verifies that a view answering from memory rather than from a query narrows the same
        /// way. The issue overview composes its rows itself, so it takes this overload.
        /// </summary>
        [Fact]
        public void Apply_OverASequence_NarrowsTheResult()
        {
            Seed(nameof(Apply_OverASequence_NarrowsTheResult));

            var filterId = Store("Name = \"Acme\"");
            var result = CustomQuickfilterSupport.Apply([filterId], Tenants().AsEnumerable(), "tenants").ToList();

            Assert.Single(result);
            Assert.Equal("Acme", result[0].Name);
        }

        /// <summary>
        /// Verifies that the sequence overload narrows what the caller already narrowed rather than
        /// starting over, mirroring the query overload.
        /// </summary>
        [Fact]
        public void Apply_OverASequence_ComposesOntoWhatWasAlreadyNarrowed()
        {
            Seed(nameof(Apply_OverASequence_ComposesOntoWhatWasAlreadyNarrowed));

            var filterId = Store("Name = \"Initech\"");
            var active = Tenants().Where(x => x.State == TenantState.Active).AsEnumerable();

            Assert.Empty(CustomQuickfilterSupport.Apply([filterId], active, "tenants").ToList());
        }

        /// <summary>
        /// Verifies that a broken expression leaves a memory-backed view usable too.
        /// </summary>
        [Fact]
        public void Apply_OverASequence_SkipsAnExpressionThatNoLongerParses()
        {
            Seed(nameof(Apply_OverASequence_SkipsAnExpressionThatNoLongerParses));

            var filterId = Store("=== not a query ===");

            Assert.Equal(3, CustomQuickfilterSupport.Apply([filterId], Tenants().AsEnumerable(), "tenants").Count());
        }
    }
}
