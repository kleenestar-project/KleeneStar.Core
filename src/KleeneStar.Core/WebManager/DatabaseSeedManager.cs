using KleeneStar.Core.WebSeed;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The database seeds of the plugins, collected by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b> <see cref="IDatabaseSeed"/>
    /// with a public parameterless constructor. The core ships none.
    /// </summary>
    /// <remarks>
    /// A seed with a blank key is skipped. The latest offer of a key is in use.
    /// <see cref="KleeneStarApplication.Run"/> reads <see cref="Seeds"/> once all plugins are
    /// registered.
    /// </remarks>
    public sealed class DatabaseSeedManager : IDatabaseSeedManager
    {
        private readonly PluginTypeCatalog<IDatabaseSeed> _catalog = new(x => Normalize(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private DatabaseSeedManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                _catalog.Collect(typeof(DatabaseSeedManager).Assembly);
            }
        }

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IDatabaseSeed> Seeds => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IDatabaseSeed seed)
        {
            ArgumentNullException.ThrowIfNull(seed);

            if (Normalize(seed.Key) is null)
            {
                throw new ArgumentException("A database seed must carry a key.", nameof(seed));
            }

            _catalog.Register(seed);
        }

        /// <inheritdoc/>
        public bool Unregister(string key)
        {
            var normalized = Normalize(key);

            return normalized is not null && _catalog.Unregister(normalized);
        }

        /// <summary>
        /// Returns the key a seed is filed under: trimmed and lower-cased, or
        /// <see langword="null"/> for a blank key.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The normalized key, or <see langword="null"/>.</returns>
        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Stops following the plugins.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }
    }
}
