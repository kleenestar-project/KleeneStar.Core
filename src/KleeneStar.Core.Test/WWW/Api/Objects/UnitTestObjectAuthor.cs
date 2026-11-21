using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WWW.Api.Objects
{
    /// <summary>
    /// Tests whom a new object is attributed to. An anonymous caller is nobody, and nobody is
    /// not a row in the identity table - writing the empty guid as the creator broke the
    /// foreign key, and the creation wizard answered <em>validation failed</em> with nothing
    /// to correct. The references stay unset for such a caller and name the identity for a
    /// signed-in one.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectAuthor
    {
        private static readonly Guid IdentityId = Guid.Parse("D4D4D4D4-0000-4000-8000-000000000001");

        /// <summary>
        /// Seeds one identity to attribute to.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Identities.Add(new Identity { Id = IdentityId, UserName = "alice", Name = "Alice", Email = "alice@example.org", PasswordHash = "$seed$" });
            db.SaveChanges();
        }

        /// <summary>
        /// Nobody is attributed as no one, not as the empty guid.
        /// </summary>
        [Fact]
        public void ResolveAuthor_AnonymousCaller_IsNull()
        {
            Seed(nameof(ResolveAuthor_AnonymousCaller_IsNull));

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.Null(global::KleeneStar.Core.WWW.Api._1_.Objects.Index.ResolveAuthor(null));
            }
        }

        /// <summary>
        /// A signed-in caller is attributed by id.
        /// </summary>
        [Fact]
        public void ResolveAuthor_SignedInCaller_IsTheIdentity()
        {
            Seed(nameof(ResolveAuthor_SignedInCaller_IsTheIdentity));

            using (CoreHub.SessionManager.BeginIdentity(IdentityId))
            {
                Assert.Equal(IdentityId, global::KleeneStar.Core.WWW.Api._1_.Objects.Index.ResolveAuthor(null));
            }
        }
    }
}
