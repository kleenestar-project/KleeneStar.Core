using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// The registry of the sources accounts are authenticated by. The core registers the
    /// internal source; a plugin extends the set by calling <see cref="Register"/> with its own
    /// <see cref="IAuthenticationSource"/>, the way it registers an object kind or a renderer.
    /// </summary>
    /// <remarks>
    /// The catalog is the semantic lookup behind the persisted
    /// <see cref="Identity.AuthenticationSource"/> key. Like the renderer catalog it does not
    /// gate persistence - an account keeps the key of a source whose plugin is gone - but it
    /// reads such an account the opposite way: an unregistered key authenticates nobody
    /// (<see cref="Resolve"/> answers <see langword="null"/>), because the only fallback there
    /// could be is the internal password check, of an account whose password was never kept
    /// here.
    /// </remarks>
    public static class AuthenticationSourceCatalog
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<string, IAuthenticationSource> _sources = new(StringComparer.Ordinal);
        private static int _version;

        /// <summary>
        /// Gets a number that changes whenever the set of registered sources does, so a cached
        /// picker can tell that a plugin registered one after it was built.
        /// </summary>
        public static int Version => Volatile.Read(ref _version);

        /// <summary>
        /// Initializes the catalog with the internal source.
        /// </summary>
        static AuthenticationSourceCatalog()
        {
            Register(new LocalAuthenticationSource());
        }

        /// <summary>
        /// Gets the registered sources, ordered by <see cref="IAuthenticationSource.Order"/> and
        /// then by key.
        /// </summary>
        public static IEnumerable<IAuthenticationSource> Sources
        {
            get
            {
                lock (_sync)
                {
                    return [.. _sources.Values
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
                }
            }
        }

        /// <summary>
        /// Registers a source. Registering a key that is present replaces the source - which is
        /// how a plugin would take over the internal source, and why the key is normalized
        /// first.
        /// </summary>
        /// <param name="source">The source. Must not be null.</param>
        public static void Register(IAuthenticationSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (string.IsNullOrWhiteSpace(source.Key))
            {
                throw new ArgumentException("An authentication source must carry a key.", nameof(source));
            }

            // the pickers offer a source under a guid derived from its key; remembering it is
            // what lets a submitted id be read back as the key
            IdentitySource.Remember(source.Key);

            lock (_sync)
            {
                _sources[KeyOf(source.Key)] = source;

                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>
        /// Removes a source, so its accounts can no longer sign in. The internal source cannot
        /// be removed, only replaced.
        /// </summary>
        /// <param name="key">The key of the source.</param>
        /// <returns><see langword="true"/> when a source was removed.</returns>
        public static bool Unregister(string key)
        {
            if (IdentitySource.IsLocal(key))
            {
                return false;
            }

            lock (_sync)
            {
                var removed = _sources.Remove(KeyOf(key));

                if (removed)
                {
                    Interlocked.Increment(ref _version);
                }

                return removed;
            }
        }

        /// <summary>
        /// Returns the source registered under a key.
        /// </summary>
        /// <param name="key">The key. A blank key names the internal source.</param>
        /// <returns>The source, or <see langword="null"/> when none is registered under it.</returns>
        public static IAuthenticationSource GetSource(string key)
        {
            lock (_sync)
            {
                return _sources.TryGetValue(KeyOf(key), out var source) ? source : null;
            }
        }

        /// <summary>
        /// Returns the source that authenticates the supplied account.
        /// </summary>
        /// <param name="account">The account. May be null.</param>
        /// <returns>The source, or <see langword="null"/> when the account is null or names a
        /// source that is not registered - in which case it signs in by no means at all.</returns>
        public static IAuthenticationSource Resolve(Identity account)
        {
            return account is null ? null : GetSource(account.AuthenticationSource);
        }

        /// <summary>
        /// Determines whether this installation owns the credentials of the supplied account,
        /// so its password may be set, changed and reset here.
        /// </summary>
        /// <remarks>
        /// An account whose source is not registered is not answered as internal: its key says
        /// somebody else owns its credentials, whether or not that somebody is installed.
        /// </remarks>
        /// <param name="account">The account. May be null, which answers false.</param>
        /// <returns><see langword="true"/> for an account of a registered, internal source.</returns>
        public static bool ManagesPassword(Identity account)
        {
            var source = Resolve(account);

            return source is not null && !source.IsExternal;
        }

        /// <summary>
        /// Returns the key a source is filed under: <see cref="IdentitySource.Local"/> for the
        /// internal source, the normalized key otherwise.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The key the catalog files it under.</returns>
        private static string KeyOf(string key)
        {
            return IdentitySource.Normalize(key) ?? IdentitySource.Local;
        }
    }
}
