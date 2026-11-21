using KleeneStar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebHealth;

namespace KleeneStar.Core.WebHealth
{
    /// <summary>
    /// Fails the health probe when the configured database cannot be reached, does not exist,
    /// or carries a schema older than the one this build expects.
    /// </summary>
    /// <remarks>
    /// The check goes through <see cref="ModelHub.CreateDbContext"/>, so it asks the provider
    /// the installation is configured with (see <c>Plugins:kleenestar.core:Database</c>) and
    /// needs no SQL of its own. Reading the migration history is the round trip: it proves the
    /// connection, and the comparison with the migrations of the provider assembly proves the
    /// schema is the one the code was written against. A database <em>ahead</em> of this build
    /// - a newer instance migrated it during a rolling update - is not a failure.
    /// <para>
    /// Existence is asked first on purpose. Opening a sqlite connection creates a missing file,
    /// and a probe must not leave an empty database behind where a volume went missing.
    /// </para>
    /// </remarks>
    [HealthTimeout(3000)]
    public sealed class DatabaseHealth : IHealth
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        private DatabaseHealth()
        {
        }

        /// <summary>
        /// Verifies the database round trip and the schema version.
        /// </summary>
        /// <param name="cancellationToken">The budget of the check.</param>
        /// <returns>The outcome, with the reason as the diagnostic for the server log.</returns>
        public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            await using var db = ModelHub.CreateDbContext();

            if (!db.Database.IsRelational())
            {
                // a provider without schema or migrations (the in-memory one of the tests)
                // can only be asked whether it answers
                return await db.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The database does not accept connections.");
            }

            var creator = db.Database.GetService<IRelationalDatabaseCreator>();

            if (!await creator.ExistsAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("The configured database does not exist.");
            }

            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            var pending = db.Database.GetMigrations()
                .Except(applied)
                .Order()
                .ToArray();

            if (pending.Length > 0)
            {
                return HealthCheckResult.Unhealthy
                (
                    $"The database schema lacks {pending.Length} migration(s), the first being '{pending[0]}'."
                );
            }

            return HealthCheckResult.Healthy();
        }
    }
}
