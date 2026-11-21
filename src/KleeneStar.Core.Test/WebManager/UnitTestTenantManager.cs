using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.TenantManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTenantManager
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
        /// Verifies that <c>Add</c> persists the tenant and that <c>GetTenant</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetTenant_RoundTrip()
        {
            Seed(nameof(Add_Then_GetTenant_RoundTrip));

            var tenant = Sample("Acme");
            CoreHub.TenantManager.Add(tenant);

            var loaded = CoreHub.TenantManager.GetTenant(tenant.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Acme", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var tenant = Sample("Initial");
            CoreHub.TenantManager.Add(tenant);

            tenant.Name = "Renamed";
            CoreHub.TenantManager.Update(tenant);

            var loaded = CoreHub.TenantManager.GetTenant(tenant.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the tenant and raises the
        /// <see cref="KleeneStar.Core.WebManager.ITenantManager.TenantRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var tenant = Sample("DeleteMe");
            CoreHub.TenantManager.Add(tenant);

            Tenant raised = null;
            CoreHub.TenantManager.TenantRemoved += (_, t) => raised = t;

            CoreHub.TenantManager.Remove(tenant.Id);

            Assert.Null(CoreHub.TenantManager.GetTenant(tenant.Id));
            Assert.NotNull(raised);
            Assert.Equal(tenant.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>GetTenants(IQuery)</c> returns tenants from the database.
        /// </summary>
        [Fact]
        public void GetTenants_ReturnsAllStored()
        {
            Seed(nameof(GetTenants_ReturnsAllStored));

            CoreHub.TenantManager.Add(Sample("Alpha"));
            CoreHub.TenantManager.Add(Sample("Beta"));

            var result = CoreHub.TenantManager.GetTenants(new Query<Tenant>()).ToList();

            Assert.True(result.Count >= 2);
            Assert.Contains(result, t => t.Name == "Alpha");
            Assert.Contains(result, t => t.Name == "Beta");
        }

        /// <summary>
        /// Creates a sample <see cref="Tenant"/> with a fresh GUID.
        /// </summary>
        /// <param name="name">The tenant name.</param>
        /// <returns>The sample tenant.</returns>
        private static Tenant Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            State = TenantState.Active
        };
    }
}
