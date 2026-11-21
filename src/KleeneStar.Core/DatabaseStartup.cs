using KleeneStar.Core.WebSeed;
using KleeneStar.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using WebExpress.WebCore.WebCluster;
using WebExpress.WebCore.WebLog;

namespace KleeneStar.Core
{
    /// <summary>
    /// Brings the database to the schema of this build and seeds it, once per start, safely
    /// when several replicas start at the same time.
    /// </summary>
    /// <remarks>
    /// Three steps race when replicas share one database, and each is guarded where it can be:
    /// <list type="bullet">
    /// <item><b>Migrate.</b> EF Core (9 and later) takes a database-wide lock inside
    /// <c>Migrate()</c> - <c>sp_getapplock</c> on SQL Server, the <c>__EFMigrationsLock</c> table
    /// on SQLite - so a second replica waits and then finds nothing pending.</item>
    /// <item><b>Seed.</b> The seeder checks and then inserts (<c>if (!db.Groups.Any())</c>), so two
    /// replicas seeding at once both insert. It runs under the same EF database lock, taken
    /// explicitly once <c>Migrate()</c> released it - the lock is not reentrant on SQLite, so it
    /// cannot span both.</item>
    /// <item><b>Legacy reset.</b> Deleting a database whose schema predates the migration history
    /// is never done for a database that has a history: whatever made a migration fail there, it
    /// is not a legacy schema, and dropping it would destroy what another replica - or the
    /// previous release - wrote.</item>
    /// </list>
    /// The whole sequence additionally runs under a cluster-wide lock
    /// (<see cref="IClusterManager.Lock"/>), which spans the steps the database lock cannot and
    /// keeps replicas from logging a burst of lock waits. It is cluster-wide only when
    /// <c>WebExpress:Cluster:StatePath</c> (or a plugin's store) is shared; otherwise it is
    /// process-local and the database lock alone guards the replicas.
    /// <para>
    /// An init job that migrates before the replicas start (an EF migration bundle against the
    /// provider assembly) needs nothing of this to change: the replicas then find the schema
    /// current and only the seed is left to them, under the lock.
    /// </para>
    /// </remarks>
    public static class DatabaseStartup
    {
        /// <summary>
        /// The name of the cluster lock the startup sequence runs under.
        /// </summary>
        public const string LockName = "kleenestar.core.database";

        /// <summary>
        /// How long the cluster lock holds when the replica holding it dies without releasing it.
        /// A migration outrunning it is still guarded by the database lock.
        /// </summary>
        public static readonly TimeSpan LockLifetime = TimeSpan.FromMinutes(15);

        /// <summary>
        /// How long a replica waits for another to finish migrating and seeding before it gives
        /// up, fails its start and lets the orchestrator restart it.
        /// </summary>
        public static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Migrates and seeds the database.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="cluster">The cluster manager, or null where none exists (tests).</param>
        /// <param name="log">The log, or null.</param>
        /// <exception cref="TimeoutException">Another replica held the startup lock past <see cref="LockTimeout"/>.</exception>
        public static void Run(KleeneStarDbContext db, IClusterManager cluster, ILog log)
        {
            using var startup = AcquireClusterLock(cluster, log);

            using (AcquireDatabaseLock(db))
            {
                ReplaceRetiredMigrations(db, log);
            }

            MigrateWithLegacyDbReset(db, log);

            using (AcquireDatabaseLock(db))
            {
                KleeneStarDbSeeder.SeedAsync(db).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Runs the database seeds of the plugins (<see cref="IDatabaseSeed"/>) in the given order,
        /// under the cluster lock <see cref="LockName"/> and the database lock the base seed runs
        /// under.
        /// </summary>
        /// <remarks>
        /// A seed that throws is logged and the next one runs; the seeds are idempotent and the next
        /// start completes what a failed one left.
        /// </remarks>
        /// <param name="db">The database context, migrated and holding the base data.</param>
        /// <param name="seeds">The seeds in execution order.</param>
        /// <param name="cluster">The cluster manager, or null where none exists (tests).</param>
        /// <param name="log">The log, or null.</param>
        /// <exception cref="TimeoutException">Another replica held the startup lock past <see cref="LockTimeout"/>.</exception>
        public static void RunSeeds(KleeneStarDbContext db, IEnumerable<IDatabaseSeed> seeds, IClusterManager cluster, ILog log)
        {
            ArgumentNullException.ThrowIfNull(db);

            var pending = seeds?.Where(x => x is not null).ToList() ?? [];

            if (pending.Count == 0)
            {
                return;
            }

            using var startup = AcquireClusterLock(cluster, log);
            using var databaseLock = AcquireDatabaseLock(db);

            foreach (var seed in pending)
            {
                try
                {
                    log?.Info($"Running the database seed '{seed.Key}'.");

                    seed.SeedAsync(db).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log?.Error($"The database seed '{seed.Key}' failed.");
                    log?.Exception(ex);

                    // entities the failed seed left tracked would be written by the next one
                    db.ChangeTracker.Clear();
                }
            }
        }

        /// <summary>
        /// Migration ids a database may have recorded that no longer exist, each with the ids
        /// that describe the same schema now.
        /// </summary>
        /// <remarks>
        /// A history that recorded <c>20261121092836_TemplateHierarchies</c> holds the whole chain
        /// <c>20261121082301_InitialCreate</c>, <c>20261121084614_TemplateChildren</c>,
        /// <c>20261121092836_TemplateHierarchies</c>, whose schema is the schema of
        /// <c>20261121193944_InitialCreate</c>; the two earlier ids stay in the history as
        /// unknown entries. A history ending at an earlier id of that chain holds an older schema
        /// and is not mapped.
        /// </remarks>
        public static readonly IReadOnlyDictionary<string, string[]> RetiredMigrations =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["20261121092836_TemplateHierarchies"] =
                [
                    "20261121193944_InitialCreate"
                ]
            };

        /// <summary>
        /// Rewrites the migration history of a database that recorded a retired migration id
        /// (<see cref="RetiredMigrations"/>) into the ids that describe the same schema, so
        /// <c>Migrate()</c> finds them applied instead of creating every table again.
        /// </summary>
        /// <remarks>
        /// Only the history rows change; the schema is left as it is. A replacement is written
        /// only when this build knows every one of its ids, so a provider with migrations of its
        /// own is never touched.
        /// </remarks>
        /// <param name="db">The database context.</param>
        /// <param name="log">The log, or null.</param>
        private static void ReplaceRetiredMigrations(KleeneStarDbContext db, ILog log)
        {
            if (!db.Database.IsRelational())
            {
                return;
            }

            var history = db.GetService<IHistoryRepository>();

            if (!history.Exists())
            {
                return;
            }

            var applied = history.GetAppliedMigrations()
                .Select(x => x.MigrationId)
                .ToHashSet(StringComparer.Ordinal);
            var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);

            foreach (var (retired, replacements) in RetiredMigrations)
            {
                if (!applied.Contains(retired) || !replacements.All(known.Contains))
                {
                    continue;
                }

                log?.Info($"Replacing the retired migration '{retired}' in the migration history by '{string.Join("', '", replacements)}'.");

                using var transaction = db.Database.BeginTransaction();

                db.Database.ExecuteSqlRaw(history.GetDeleteScript(retired));

                foreach (var replacement in replacements.Where(x => !applied.Contains(x)))
                {
                    db.Database.ExecuteSqlRaw(history.GetInsertScript(new HistoryRow(replacement, ProductInfo.GetVersion())));
                    applied.Add(replacement);
                }

                transaction.Commit();
                applied.Remove(retired);
            }
        }

        /// <summary>
        /// Takes the cluster-wide startup lock.
        /// </summary>
        /// <param name="cluster">The cluster manager, or null.</param>
        /// <param name="log">The log, or null.</param>
        /// <returns>The lock, or null when there is no cluster manager.</returns>
        private static IDisposable AcquireClusterLock(IClusterManager cluster, ILog log)
        {
            if (cluster is null)
            {
                return null;
            }

            if (cluster.IsClustered)
            {
                log?.Info($"Acquiring the database startup lock on node '{cluster.NodeId}'.");
            }

            return cluster.Lock(LockName, LockLifetime, LockTimeout)
                ?? throw new TimeoutException
                (
                    $"Another instance held the database startup lock '{LockName}' for more than " +
                    $"{LockTimeout.TotalMinutes:0} minutes. If no instance is migrating, the lock is left " +
                    $"over from one that died; it expires after {LockLifetime.TotalMinutes:0} minutes."
                );
        }

        /// <summary>
        /// Takes the database-wide lock EF Core migrates under, so the seed of one replica never
        /// interleaves with the migration or the seed of another.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <returns>The lock, or null for a provider that has none.</returns>
        private static IDisposable AcquireDatabaseLock(KleeneStarDbContext db)
        {
            if (!db.Database.IsRelational())
            {
                return null;
            }

            var history = db.GetService<IHistoryRepository>();

            // a lock released with its transaction would need the seed inside one transaction,
            // which the seeder does not run in; none of the shipped providers does that
            if (history.LockReleaseBehavior == LockReleaseBehavior.Transaction)
            {
                return null;
            }

            // a lock held by the connection (sql server) is gone the moment the connection goes
            // back to the pool, so the connection is pinned open for as long as the lock is held
            db.Database.OpenConnection();

            try
            {
                var databaseLock = history.AcquireDatabaseLock();

                return new Release(() =>
                {
                    try
                    {
                        databaseLock.Dispose();
                    }
                    finally
                    {
                        db.Database.CloseConnection();
                    }
                });
            }
            catch
            {
                db.Database.CloseConnection();

                throw;
            }
        }

        /// <summary>
        /// Applies pending migrations. When the database exists but its schema was
        /// previously created without a <c>__EFMigrationsHistory</c> table (for example
        /// by an older <c>EnsureCreated()</c> code path or by a developer manually
        /// editing the migration files), the first migration throws "table already
        /// exists". In that case the database is reset and the migration is retried —
        /// the seeder will then repopulate every row.
        /// </summary>
        /// <remarks>
        /// The reset happens only for a database without any recorded migration. A database
        /// with a history failing the same way has a broken migration, not a legacy schema;
        /// the error is left to escape, so the start fails and nothing is deleted.
        /// </remarks>
        /// <param name="db">The database context.</param>
        /// <param name="log">The log used to write a warning entry, or null.</param>
        private static void MigrateWithLegacyDbReset(KleeneStarDbContext db, ILog log)
        {
            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex) when (IsAlreadyExistsError(ex) && !db.Database.GetAppliedMigrations().Any())
            {
                log?.Warning
                (
                    "Legacy database schema without migrations history detected. " +
                    "Resetting the database and re-running migrations + seed."
                );

                db.Database.EnsureDeleted();
                db.Database.Migrate();
            }
            catch (Exception ex) when (IsAlreadyExistsError(ex) && !db.Database.GetAppliedMigrations().Intersect(db.Database.GetMigrations()).Any())
            {
                var recorded = string.Join("', '", db.Database.GetAppliedMigrations());

                throw new InvalidOperationException
                (
                    $"The database was created by an earlier schema (migration '{recorded}') that this " +
                    "build cannot upgrade: none of its recorded migrations is known here. Back up the " +
                    "database and start with an empty one, or run the release that created it.",
                    ex
                );
            }
        }

        /// <summary>
        /// Returns whether the supplied exception (or any of its inner exceptions)
        /// reports the "table already exists" condition that providers raise when
        /// the migration tries to (re-)create a table that is already present in
        /// the database.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns><c>true</c> when the message chain contains "already exists".</returns>
        private static bool IsAlreadyExistsError(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is DbException &&
                    current.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Runs an action once on dispose.
        /// </summary>
        /// <param name="release">The action.</param>
        private sealed class Release(Action release) : IDisposable
        {
            private Action _release = release;

            /// <summary>
            /// Runs the action, the first time only.
            /// </summary>
            public void Dispose()
            {
                System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke();
            }
        }
    }
}
