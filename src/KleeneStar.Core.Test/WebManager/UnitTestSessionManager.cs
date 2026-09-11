using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.SessionManager"/>.
    /// Exercises both the generic (owner, scope, key) key/value façade and the
    /// strongly-typed REST API table layout helpers built on top of it.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSessionManager
    {
        private static readonly Guid OwnerId = Guid.Parse("AA112233-4455-6677-8899-AABBCCDDEEFF");
        private const string Scope = "rest-table-layout";

        /// <summary>
        /// Seeds the in-memory database with a single identity that owns the
        /// session entries created by each test case, and begins acting as it - the
        /// request-less overloads read the acting identity, which a request would otherwise
        /// have named.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <returns>The acting scope; closing it stops acting as that identity.</returns>
        private static IDisposable Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Identities.Any(x => x.Id == OwnerId))
            {
                db.Identities.Add(new Identity
                {
                    Id = OwnerId,
                    Name = "Test Owner",
                    Email = "owner@kleenestar.test",
                    PasswordHash = "$seed$v1$test"
                });
            }

            db.SaveChanges();

            return CoreHub.SessionManager.BeginIdentity(OwnerId);
        }

        /// <summary>
        /// Verifies that <c>SetValue</c> followed by <c>GetValue</c> returns the
        /// originally written payload.
        /// </summary>
        [Fact]
        public void SetValue_Then_GetValue_RoundTrip()
        {
            using var acting = Seed(nameof(SetValue_Then_GetValue_RoundTrip));

            CoreHub.SessionManager.SetValue(OwnerId, Scope, "MyTable", "{\"x\":1}");

            var value = CoreHub.SessionManager.GetValue(OwnerId, Scope, "MyTable");

            Assert.Equal("{\"x\":1}", value);
        }

        /// <summary>
        /// Verifies that <c>GetValue</c> returns <c>null</c> when nothing has
        /// been stored for the requested (owner, scope, key) tuple.
        /// </summary>
        [Fact]
        public void GetValue_Missing_ReturnsNull()
        {
            using var acting = Seed(nameof(GetValue_Missing_ReturnsNull));

            var value = CoreHub.SessionManager.GetValue(OwnerId, Scope, "Unknown");

            Assert.Null(value);
        }

        /// <summary>
        /// Verifies that writing twice with the same key overwrites the previous
        /// value (the unique index on (Owner, Scope, Key) means there is at most
        /// one row per tuple).
        /// </summary>
        [Fact]
        public void SetValue_Twice_OverwritesPrevious()
        {
            using var acting = Seed(nameof(SetValue_Twice_OverwritesPrevious));

            CoreHub.SessionManager.SetValue(OwnerId, Scope, "MyTable", "first");
            CoreHub.SessionManager.SetValue(OwnerId, Scope, "MyTable", "second");

            var value = CoreHub.SessionManager.GetValue(OwnerId, Scope, "MyTable");

            Assert.Equal("second", value);
        }

        /// <summary>
        /// Verifies that <c>SetValue</c> with a <c>null</c> payload removes the
        /// stored row, leaving subsequent reads with no value.
        /// </summary>
        [Fact]
        public void SetValue_Null_DeletesEntry()
        {
            using var acting = Seed(nameof(SetValue_Null_DeletesEntry));

            CoreHub.SessionManager.SetValue(OwnerId, Scope, "MyTable", "payload");
            CoreHub.SessionManager.SetValue(OwnerId, Scope, "MyTable", null);

            var value = CoreHub.SessionManager.GetValue(OwnerId, Scope, "MyTable");

            Assert.Null(value);
        }

        /// <summary>
        /// Verifies that the round-trip for the REST API table layout serializes
        /// the user-defined columns to JSON and reconstitutes them on read,
        /// preserving id / visibility / width and order.
        /// </summary>
        [Fact]
        public void SetTableLayout_Then_GetTableLayout_RoundTrip()
        {
            using var acting = Seed(nameof(SetTableLayout_Then_GetTableLayout_RoundTrip));

            const string tableKey = "MyNamespace.MyTable";
            var columns = new[]
            {
                new RestApiTableColumn { Id = "name", Visible = true,  Width = 200 },
                new RestApiTableColumn { Id = "state", Visible = false, Width = null },
                new RestApiTableColumn { Id = "key", Visible = true,  Width = 80 }
            };

            CoreHub.SessionManager.SetTableLayout(null, tableKey, columns);

            var stored = CoreHub.SessionManager.GetTableLayout(null, tableKey);

            Assert.NotNull(stored);
            Assert.Equal(new[] { "name", "state", "key" }, stored.Select(c => c.Id).ToArray());
            Assert.Equal(true, stored[0].Visible);
            Assert.Equal(200u, stored[0].Width);
            Assert.Equal(false, stored[1].Visible);
            Assert.Null(stored[1].Width);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.ISessionManager.ApplyStoredTableLayout"/>
        /// reorders the default columns to match the stored layout and forwards
        /// the persisted visibility / width onto the returned column instances.
        /// </summary>
        [Fact]
        public void ApplyStoredTableLayout_ReordersAndAppliesWidthAndVisibility()
        {
            using var acting = Seed(nameof(ApplyStoredTableLayout_ReordersAndAppliesWidthAndVisibility));

            const string tableKey = "MyNamespace.MyTable";
            var defaults = new[]
            {
                new RestApiTableColumn { Id = "key",   Label = "Key",   Visible = true  },
                new RestApiTableColumn { Id = "name",  Label = "Name",  Visible = true  },
                new RestApiTableColumn { Id = "state", Label = "State", Visible = false }
            };

            var storedLayout = new[]
            {
                new RestApiTableColumn { Id = "name", Visible = true,  Width = 250 },
                new RestApiTableColumn { Id = "key",  Visible = false, Width = 90 }
            };

            CoreHub.SessionManager.SetTableLayout(null, tableKey, storedLayout);

            var result = CoreHub.SessionManager
                .ApplyStoredTableLayout(null, tableKey, defaults)
                .ToList();

            // ordered: stored first ("name", "key"), then unmentioned defaults ("state")
            Assert.Equal(new[] { "name", "key", "state" }, result.Select(c => c.Id).ToArray());

            // labels remain owned by defaults
            Assert.Equal("Name",  result[0].Label);
            Assert.Equal("Key",   result[1].Label);
            Assert.Equal("State", result[2].Label);

            // visibility/width forwarded from stored layout
            Assert.True(result[0].Visible);
            Assert.Equal(250u, result[0].Width);
            Assert.False(result[1].Visible);
            Assert.Equal(90u, result[1].Width);

            // default kept for columns absent from the stored layout
            Assert.False(result[2].Visible);
            Assert.Null(result[2].Width);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.ISessionManager.ApplyStoredTableLayout"/>
        /// returns the defaults verbatim when nothing has been stored for the
        /// given table key.
        /// </summary>
        [Fact]
        public void ApplyStoredTableLayout_NothingStored_ReturnsDefaults()
        {
            using var acting = Seed(nameof(ApplyStoredTableLayout_NothingStored_ReturnsDefaults));

            var defaults = new[]
            {
                new RestApiTableColumn { Id = "a", Visible = true  },
                new RestApiTableColumn { Id = "b", Visible = false }
            };

            var result = CoreHub.SessionManager
                .ApplyStoredTableLayout(null, "Unknown.Table", defaults)
                .ToList();

            Assert.Equal(new[] { "a", "b" }, result.Select(c => c.Id).ToArray());
            Assert.True(result[0].Visible);
            Assert.False(result[1].Visible);
        }

        /// <summary>
        /// Verifies that scope acts as a namespace: entries written under one
        /// scope are not visible when reading from another scope, even when
        /// the key matches.
        /// </summary>
        [Fact]
        public void GetValue_DifferentScope_DoesNotLeak()
        {
            using var acting = Seed(nameof(GetValue_DifferentScope_DoesNotLeak));

            CoreHub.SessionManager.SetValue(OwnerId, "scope-a", "shared-key", "a");
            CoreHub.SessionManager.SetValue(OwnerId, "scope-b", "shared-key", "b");

            Assert.Equal("a", CoreHub.SessionManager.GetValue(OwnerId, "scope-a", "shared-key"));
            Assert.Equal("b", CoreHub.SessionManager.GetValue(OwnerId, "scope-b", "shared-key"));
        }

        /// <summary>
        /// Verifies the identity a request-less caller is answered with: the one the enclosing
        /// scope acts as, and nobody at all outside every scope.
        /// </summary>
        /// <remarks>
        /// This is what a notification raised deep inside a manager, or an audit event nobody
        /// named an actor for, is attributed to. Answering <see cref="Guid.Empty"/> outside a
        /// scope is the point: it used to answer with the seeded administrator, which turned
        /// "nobody knows who did this" into a statement about a real person.
        /// </remarks>
        [Fact]
        public void GetCurrentIdentityId_WithoutRequest_FollowsTheActingScope()
        {
            using var acting = Seed(nameof(GetCurrentIdentityId_WithoutRequest_FollowsTheActingScope));

            Assert.Equal(OwnerId, CoreHub.SessionManager.GetCurrentIdentityId(null));

            acting.Dispose();

            Assert.Equal(Guid.Empty, CoreHub.SessionManager.GetCurrentIdentityId(null));
        }

        /// <summary>
        /// Verifies that acting scopes nest and put back what they found, so a scope may be
        /// opened without knowing what its caller did - and that acting as nobody is a
        /// statement of its own rather than a way of keeping the enclosing identity.
        /// </summary>
        [Fact]
        public void BeginIdentity_NestsAndRestores()
        {
            using var acting = Seed(nameof(BeginIdentity_NestsAndRestores));

            var other = Guid.Parse("BB223344-5566-7788-99AA-BBCCDDEEFF00");

            using (CoreHub.SessionManager.BeginIdentity(other))
            {
                Assert.Equal(other, CoreHub.SessionManager.GetCurrentIdentityId(null));

                using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
                {
                    Assert.Equal(Guid.Empty, CoreHub.SessionManager.GetCurrentIdentityId(null));
                }

                Assert.Equal(other, CoreHub.SessionManager.GetCurrentIdentityId(null));
            }

            Assert.Equal(OwnerId, CoreHub.SessionManager.GetCurrentIdentityId(null));
        }

        /// <summary>
        /// Verifies that a preference written for nobody is not written at all: the
        /// convenience overloads resolve the identity first, and there is no row to own the
        /// entry when that answers nobody.
        /// </summary>
        [Fact]
        public void SetValue_WithoutAnIdentity_WritesNothing()
        {
            using var acting = Seed(nameof(SetValue_WithoutAnIdentity_WritesNothing));

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                CoreHub.SessionManager.SetValue(null, Scope, "MyTable", "[]");

                Assert.Null(CoreHub.SessionManager.GetValue(null, Scope, "MyTable"));
            }

            Assert.Null(CoreHub.SessionManager.GetValue(OwnerId, Scope, "MyTable"));
        }
    }
}