using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace KleeneStar.Core.WebMaintenance
{
    /// <summary>
    /// A backup of the database.
    /// </summary>
    /// <param name="File">The file name.</param>
    /// <param name="Size">The size in bytes.</param>
    /// <param name="Created">When it was written, in UTC.</param>
    /// <param name="Integrity"><c>ok</c>, or what the check found when it was written.</param>
    public sealed record DatabaseBackup(string File, long Size, DateTime Created, string Integrity);

    /// <summary>
    /// The backups of a SQLite installation: written on a schedule and on demand, checked when
    /// written, and kept to a number (<c>Database:BackupDirectory</c>, <c>BackupIntervalHours</c>,
    /// <c>BackupKeep</c>).
    /// </summary>
    /// <remarks>
    /// A backup is a consistent copy taken while the installation runs. It is checked with
    /// SQLite's integrity check before it counts: a backup that does not pass is removed rather
    /// than kept as a promise it cannot keep. Restoring is the copy put back while the
    /// installation is stopped (see <c>kleenestar.recovery.md</c>); the search index rebuilds
    /// itself from the database.
    /// </remarks>
    public static class DatabaseBackups
    {
        /// <summary>
        /// The prefix of a backup's file name.
        /// </summary>
        public const string Prefix = "kleenestar-";

        /// <summary>
        /// Gets the directory the backups are written to.
        /// </summary>
        public static string Directory => Path.GetFullPath(ModelHub.DatabaseSettings?.BackupDirectory ?? "data/backup");

        /// <summary>
        /// Returns the backups, the newest first.
        /// </summary>
        /// <returns>The backups.</returns>
        public static IReadOnlyList<DatabaseBackup> List()
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return [];
            }

            return [.. new DirectoryInfo(Directory)
                .GetFiles(Prefix + "*.db")
                .OrderByDescending(x => x.Name, StringComparer.Ordinal)
                .Select(x => new DatabaseBackup(x.Name, x.Length, x.CreationTimeUtc, null))];
        }

        /// <summary>
        /// Writes a backup now, checks it, and removes the ones beyond the number kept.
        /// </summary>
        /// <param name="now">The moment, in UTC.</param>
        /// <returns>The backup.</returns>
        /// <exception cref="NotSupportedException">The database is not SQLite.</exception>
        /// <exception cref="InvalidOperationException">The copy did not pass the integrity check.</exception>
        public static DatabaseBackup Create(DateTime now)
        {
            var name = Prefix + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".db";
            var file = Path.Combine(Directory, name);

            ModelHub.Backup(file);

            var integrity = ModelHub.CheckIntegrity(file);

            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);

                throw new InvalidOperationException($"The backup did not pass the integrity check: {integrity}");
            }

            Prune(Math.Max(1, ModelHub.DatabaseSettings?.BackupKeep ?? 14));

            return new DatabaseBackup(name, new FileInfo(file).Length, now, integrity);
        }

        /// <summary>
        /// Writes a backup when the last one is older than the interval.
        /// </summary>
        /// <param name="now">The moment, in UTC.</param>
        /// <returns>The backup written, or <see langword="null"/>.</returns>
        public static DatabaseBackup RunDue(DateTime now)
        {
            var hours = ModelHub.DatabaseSettings?.BackupIntervalHours ?? 0;

            if (hours <= 0 || !ModelHub.IsSqlite)
            {
                return null;
            }

            var last = List().FirstOrDefault();

            return last is not null && last.Created.AddHours(hours) > now ? null : Create(now);
        }

        /// <summary>
        /// Removes the oldest backups beyond a number.
        /// </summary>
        /// <param name="keep">The number kept.</param>
        private static void Prune(int keep)
        {
            foreach (var backup in List().Skip(keep))
            {
                File.Delete(Path.Combine(Directory, backup.File));
            }
        }
    }
}
