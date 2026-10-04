using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// The registry of insight types. The core registers the dashboard; a plugin extends the
    /// set by calling <see cref="Register"/> with its own <see cref="IInsightType"/>, the way it
    /// registers an object kind, a renderer or an authentication source. There is deliberately
    /// no enum of insight types.
    /// </summary>
    /// <remarks>
    /// The catalog is the lookup behind the persisted <see cref="Model.Entities.Insight.Type"/>
    /// key and the authority the create endpoint asks: a type nobody registered cannot be
    /// chosen. It does not gate what is already stored - an insight keeps the key of a type
    /// whose plugin is gone, and its page says so instead of drawing nothing
    /// (<see cref="InsightTypeCondition"/>).
    /// </remarks>
    public static class InsightTypeCatalog
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<string, IInsightType> _types = new(StringComparer.Ordinal);
        private static int _version;

        /// <summary>
        /// Gets the key of the type a create that names none falls back to.
        /// </summary>
        public static string Default => Model.Entities.Insight.DashboardType;

        /// <summary>
        /// Gets a number that changes whenever the set of registered types does, so a cached
        /// picker can tell that a plugin registered one after it was built.
        /// </summary>
        public static int Version => Volatile.Read(ref _version);

        /// <summary>
        /// Initializes the catalog with the dashboard type.
        /// </summary>
        static InsightTypeCatalog()
        {
            Register(new DashboardInsightType());
        }

        /// <summary>
        /// Gets the registered types, ordered by <see cref="IInsightType.Order"/> and then by key.
        /// </summary>
        public static IEnumerable<IInsightType> Types
        {
            get
            {
                lock (_sync)
                {
                    return [.. _types.Values
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
                }
            }
        }

        /// <summary>
        /// Registers a type. Registering a key that is present replaces the type.
        /// </summary>
        /// <param name="type">The type. Must not be null and must carry a key.</param>
        public static void Register(IInsightType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            var key = Normalize(type.Key)
                ?? throw new ArgumentException("An insight type must carry a key.", nameof(type));

            lock (_sync)
            {
                _types[key] = type;

                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>
        /// Removes a type, so it can no longer be chosen for a new insight. The dashboard type
        /// cannot be removed, only replaced - it is what a create falls back to.
        /// </summary>
        /// <param name="key">The key of the type.</param>
        /// <returns><see langword="true"/> when a type was removed.</returns>
        public static bool Unregister(string key)
        {
            var normalized = Normalize(key);

            if (normalized is null || normalized == Default)
            {
                return false;
            }

            lock (_sync)
            {
                var removed = _types.Remove(normalized);

                if (removed)
                {
                    Interlocked.Increment(ref _version);
                }

                return removed;
            }
        }

        /// <summary>
        /// Returns the type registered under a key.
        /// </summary>
        /// <param name="key">The key, compared without case.</param>
        /// <returns>The type, or <see langword="null"/> when none is registered under it.</returns>
        public static IInsightType Get(string key)
        {
            var normalized = Normalize(key);

            if (normalized is null)
            {
                return null;
            }

            lock (_sync)
            {
                return _types.TryGetValue(normalized, out var type) ? type : null;
            }
        }

        /// <summary>
        /// Determines whether a type is registered under a key.
        /// </summary>
        /// <param name="key">The key, compared without case.</param>
        /// <returns><see langword="true"/> when the key names a registered type.</returns>
        public static bool IsRegistered(string key)
        {
            return Get(key) is not null;
        }

        /// <summary>
        /// Determines whether an insight is of the supplied type.
        /// </summary>
        /// <param name="insight">The insight. May be null, which answers false.</param>
        /// <param name="key">The key of the type, compared without case.</param>
        /// <returns><see langword="true"/> when the insight carries that type.</returns>
        public static bool IsOfType(Model.Entities.Insight insight, string key)
        {
            return insight is not null && Normalize(insight.Type) == Normalize(key);
        }

        /// <summary>
        /// Returns the key a type is filed and persisted under: trimmed and lower-cased, or
        /// <see langword="null"/> for a blank key.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The normalized key, or <see langword="null"/>.</returns>
        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
        }
    }
}
