using KleeneStar.Model;
using KleeneStar.Model.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebExpress.WebCore.WebCluster;

namespace KleeneStar.Core.Test
{
    /// <summary>
    /// Provides unit tests for <see cref="DatabaseStartup"/> against a real sqlite file, the
    /// provider whose migration lock is a table rather than a server-side lock.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestDatabaseStartup : IDisposable
    {
        private readonly DatabaseSettings _previous = ModelHub.DatabaseSettings;
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"kleenestar-startup-{Guid.NewGuid():N}.db");

        /// <summary>
        /// Initializes a new instance of the class and points the model at a fresh sqlite file.
        /// </summary>
        public UnitTestDatabaseStartup()
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
        /// Tests that replicas starting at the same time against one database all come up and
        /// seed it once - the seeder checks and then inserts, so without the lock both insert.
        /// </summary>
        [Fact]
        public async Task ConcurrentStartsSeedOnce()
        {
            var starts = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                using var db = ModelHub.CreateDbContext();

                DatabaseStartup.Run(db, null, null);
            }));

            await Task.WhenAll(starts);

            using var check = ModelHub.CreateDbContext();

            Assert.Empty(check.Database.GetPendingMigrations());
            Assert.NotEmpty(check.Groups);
            Assert.Equal(check.Groups.Count(), check.Groups.Select(x => x.Name).Distinct().Count());
            Assert.Equal(check.Identities.Count(), check.Identities.Select(x => x.Name).Distinct().Count());
            Assert.Equal(check.Workspaces.Count(), check.Workspaces.Select(x => x.Key).Distinct().Count());
            Assert.Equal(check.Tenants.Count(), check.Tenants.Select(x => x.Name).Distinct().Count());
        }

        /// <summary>
        /// Tests that a database whose schema predates the migration history is reset and
        /// migrated, as before.
        /// </summary>
        [Fact]
        public void LegacySchemaWithoutHistoryIsReset()
        {
            Execute("CREATE TABLE Groups (Id INTEGER PRIMARY KEY);");

            using var db = ModelHub.CreateDbContext();

            DatabaseStartup.Run(db, null, null);

            Assert.Empty(db.Database.GetPendingMigrations());
            Assert.NotEmpty(db.Groups);
        }

        /// <summary>
        /// Tests that a database with a migration history is never deleted, even when a
        /// migration fails the way a legacy schema does - that is a broken migration, and the
        /// data belongs to a running installation.
        /// </summary>
        [Fact]
        public void DatabaseWithHistoryIsNeverReset()
        {
            using (var db = ModelHub.CreateDbContext())
            {
                DatabaseStartup.Run(db, null, null);
            }

            // the history now claims a migration nobody shipped and forgets the real one, so
            // the real one runs again and finds its tables
            Execute
            (
                "DELETE FROM __EFMigrationsHistory; " +
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('00000000000000_Phantom', '10.0.0'); " +
                "CREATE TABLE Marker (Id INTEGER PRIMARY KEY); INSERT INTO Marker (Id) VALUES (42);"
            );

            using (var db = ModelHub.CreateDbContext())
            {
                Assert.ThrowsAny<Exception>(() => DatabaseStartup.Run(db, null, null));
            }

            Assert.Equal(42L, Scalar("SELECT Id FROM Marker;"));
        }

        /// <summary>
        /// Tests that a replica which cannot get the startup lock fails its start without
        /// touching the database.
        /// </summary>
        [Fact]
        public void HeldClusterLockFailsTheStart()
        {
            using var db = ModelHub.CreateDbContext();

            Assert.Throws<TimeoutException>(() => DatabaseStartup.Run(db, new HeldClusterManager(), null));

            Assert.False(File.Exists(_path) && Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';") is long count && count > 0);
        }

        /// <summary>
        /// Runs sql against the test database outside of any context.
        /// </summary>
        /// <param name="sql">The statements.</param>
        private void Execute(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={_path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Reads one value from the test database outside of any context.
        /// </summary>
        /// <param name="sql">The query.</param>
        /// <returns>The value.</returns>
        private object Scalar(string sql)
        {
            using var connection = new SqliteConnection($"Data Source={_path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            return command.ExecuteScalar();
        }

        /// <summary>
        /// A cluster whose startup lock another instance holds for good.
        /// </summary>
        private sealed class HeldClusterManager : IClusterManager
        {
            public string NodeId => "test";

            public bool IsClustered => true;

            public IClusterStore Store => null;

            public IClusterTransport Transport => null;

            public IReadOnlyDictionary<string, TimeSpan> ClockSkew => new Dictionary<string, TimeSpan>();

            public IDisposable Lock(string name, TimeSpan lifetime, TimeSpan timeout) => null;

            public Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public IDisposable Subscribe(string topic, Action<ClusterMessage> handler) => null;

            public void UseStore(IClusterStore store)
            {
            }

            public void UseTransport(IClusterTransport transport)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
