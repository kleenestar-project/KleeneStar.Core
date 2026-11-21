using KleeneStar.Model;
using System.Threading.Tasks;

namespace KleeneStar.Core.WebSeed
{
    /// <summary>
    /// Data a plugin writes into the database at start, after the core migrated the schema and
    /// wrote its base data (<see cref="KleeneStarDbSeeder.SeedAsync"/>).
    /// </summary>
    /// <remarks>
    /// Collected by <see cref="WebManager.DatabaseSeedManager"/>: every <b>public sealed</b>
    /// top-level implementation with a public parameterless constructor in a plugin assembly.
    /// <see cref="KleeneStarApplication.Run"/> runs the seeds in ascending <see cref="Order"/>,
    /// then by <see cref="Key"/>, under the cluster lock and the database lock of
    /// <see cref="DatabaseStartup"/>, before the hierarchy verifier, the path index rebuild and
    /// the home page check. A seed runs on every start of every replica and is therefore written
    /// idempotent: each part is guarded by a check on the store. A seed that throws is logged;
    /// the remaining seeds and the start continue.
    /// </remarks>
    public interface IDatabaseSeed
    {
        /// <summary>
        /// Returns the key of the seed, unique across the plugins (for example the plugin id).
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Returns the position of the seed among the seeds of all plugins; lower runs first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Writes the data the store does not carry yet.
        /// </summary>
        /// <param name="db">The database context, migrated and holding the base data.</param>
        /// <returns>A task representing the asynchronous seeding.</returns>
        Task SeedAsync(KleeneStarDbContext db);
    }
}
