using KleeneStar.Core.WebSeed;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The database seeds the plugins contribute (<see cref="IDatabaseSeed"/>).
    /// </summary>
    public interface IDatabaseSeedManager : IComponentManager
    {
        /// <summary>
        /// Returns a number that changes whenever a seed is added or removed.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Returns the seeds in execution order: ascending <see cref="IDatabaseSeed.Order"/>,
        /// then by <see cref="IDatabaseSeed.Key"/>.
        /// </summary>
        IEnumerable<IDatabaseSeed> Seeds { get; }

        /// <summary>
        /// Registers a seed that is no plugin type (a test stand-in).
        /// </summary>
        /// <param name="seed">The seed. Its key must not be blank.</param>
        void Register(IDatabaseSeed seed);

        /// <summary>
        /// Removes a seed registered through <see cref="Register"/>.
        /// </summary>
        /// <param name="key">The key of the seed.</param>
        /// <returns><see langword="true"/> when a seed was removed.</returns>
        bool Unregister(string key);
    }
}
