using KleeneStar.Core.WebHealth;
using KleeneStar.Model;
using KleeneStar.Model.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebExpress.WebCore.WebHealth;

namespace KleeneStar.Core.Test.WebHealth
{
    /// <summary>
    /// Provides unit tests for <see cref="DatabaseHealth"/> against a real sqlite file through
    /// the provider assembly the host ships with - the in-memory provider of the other tests
    /// has neither a schema nor migrations to judge.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestDatabaseHealth : IDisposable
    {
        private readonly DatabaseSettings _previous = ModelHub.DatabaseSettings;
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"kleenestar-health-{Guid.NewGuid():N}.db");

        /// <summary>
        /// Initializes a new instance of the class and points the model at a fresh sqlite file.
        /// </summary>
        public UnitTestDatabaseHealth()
        {
            ModelHub.DatabaseSettings = new DatabaseSettings
            {
                ConnectionString = $"Data Source={_path}"
            };
        }

        /// <summary>
        /// Restores the database settings and removes the sqlite file with its journal.
        /// </summary>
        public void Dispose()
        {
            ModelHub.DatabaseSettings = _previous;
            SqliteConnection.ClearAllPools();

            foreach (var file in new[] { _path, $"{_path}-wal", $"{_path}-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Tests that a migrated database is healthy.
        /// </summary>
        [Fact]
        public async Task MigratedDatabaseIsHealthy()
        {
            Migrate();

            var result = await Check();

            Assert.True(result.IsHealthy, result.Description);
        }

        /// <summary>
        /// Tests that a database missing migrations of this build is unhealthy and names the
        /// first one missing.
        /// </summary>
        [Fact]
        public async Task DatabaseWithoutTheSchemaIsUnhealthy()
        {
            using (var connection = new SqliteConnection($"Data Source={_path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Placeholder (Id INTEGER PRIMARY KEY);";
                command.ExecuteNonQuery();
            }

            var result = await Check();

            Assert.False(result.IsHealthy);
            Assert.Contains("migration", result.Description);
        }

        /// <summary>
        /// Tests that a database a newer build has migrated further - a rolling update - is
        /// still healthy for this one.
        /// </summary>
        [Fact]
        public async Task DatabaseAheadOfTheBuildIsHealthy()
        {
            Migrate();

            using (var db = ModelHub.CreateDbContext())
            {
                db.Database.ExecuteSqlRaw
                (
                    "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99990101000000_FromTheFuture', '10.0.0');"
                );
            }

            var result = await Check();

            Assert.True(result.IsHealthy, result.Description);
        }

        /// <summary>
        /// Tests that a database file that went missing after the start - a volume that was
        /// not mounted any more - is unhealthy, and that the probe does not create an empty
        /// database in its place.
        /// </summary>
        [Fact]
        public async Task VanishedDatabaseIsUnhealthyAndNotRecreated()
        {
            Migrate();
            SqliteConnection.ClearAllPools();
            File.Delete(_path);
            File.Delete($"{_path}-wal");
            File.Delete($"{_path}-shm");

            var result = await Check();

            Assert.False(result.IsHealthy);
            Assert.Contains("does not exist", result.Description);
            Assert.False(File.Exists(_path));
        }

        /// <summary>
        /// Tests that a provider that cannot be loaded fails the check - the framework reads an
        /// exception from a check as unhealthy and writes it to the server log.
        /// </summary>
        [Fact]
        public async Task UnknownProviderFailsTheCheck()
        {
            ModelHub.DatabaseSettings = new DatabaseSettings
            {
                Assembly = "KleeneStar.Model.DoesNotExist",
                ConnectionString = $"Data Source={_path}"
            };

            await Assert.ThrowsAnyAsync<Exception>(Check);
        }

        /// <summary>
        /// Tests that a provider without schema support is judged by whether it answers.
        /// </summary>
        [Fact]
        public async Task NonRelationalProviderIsJudgedByItsConnection()
        {
            CoreHubFixture.Initialize($"health-{Guid.NewGuid():N}");

            var result = await Check();

            Assert.True(result.IsHealthy, result.Description);
        }

        /// <summary>
        /// Applies every migration of the sqlite provider to the test file.
        /// </summary>
        private static void Migrate()
        {
            using var db = ModelHub.CreateDbContext();
            db.Database.Migrate();
        }

        /// <summary>
        /// Runs the check the way the framework activates it: through its private constructor.
        /// </summary>
        /// <returns>The result of the check.</returns>
        private static Task<HealthCheckResult> Check()
        {
            var health = (IHealth)Activator.CreateInstance(typeof(DatabaseHealth), nonPublic: true);

            return health.CheckAsync(TestContext.Current.CancellationToken);
        }
    }
}
