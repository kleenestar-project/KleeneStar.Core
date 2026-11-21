using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
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
    /// The authentication sources of the installation, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b>
    /// <see cref="IAuthenticationSource"/> with a public parameterless constructor of any plugin
    /// (the core's internal source), plus the sources registered per configuration entry. There is
    /// no enum of sources. Formerly the static <c>AuthenticationSourceCatalog</c>.
    /// </summary>
    public sealed class AuthenticationSourceManager : IAuthenticationSourceManager
    {
        private readonly PluginTypeCatalog<IAuthenticationSource> _catalog = new(x => string.IsNullOrWhiteSpace(x.Key) ? null : KeyOf(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AuthenticationSourceManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            // the pickers offer a source under a guid derived from its key; remembering every
            // source that comes into use is what lets a submitted id be read back as the key
            _catalog.Added += (_, e) => IdentitySource.Remember(e.Item.Key);

            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                // without a plugin manager (a test, a call before the host is up) the manager
                // knows its own assembly's sources
                _catalog.Collect(typeof(AuthenticationSourceManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host, for <see cref="CoreHub"/> before or without one.
        /// </summary>
        /// <returns>A manager knowing the internal source.</returns>
        internal static AuthenticationSourceManager CreateDetached()
        {
            return new AuthenticationSourceManager(null, null);
        }

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IAuthenticationSource> Sources => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IAuthenticationSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (string.IsNullOrWhiteSpace(source.Key))
            {
                throw new ArgumentException("An authentication source must carry a key.", nameof(source));
            }

            _catalog.Register(source);
        }

        /// <inheritdoc/>
        public bool Unregister(string key)
        {
            return !IdentitySource.IsLocal(key) && _catalog.Unregister(KeyOf(key));
        }

        /// <inheritdoc/>
        public IAuthenticationSource GetSource(string key)
        {
            return _catalog.Find(KeyOf(key));
        }

        /// <inheritdoc/>
        public IAuthenticationSource Resolve(Identity account)
        {
            return account is null ? null : GetSource(account.AuthenticationSource);
        }

        /// <inheritdoc/>
        public bool ManagesPassword(Identity account)
        {
            var source = Resolve(account);

            return source is not null && !source.IsExternal;
        }

        /// <summary>
        /// Stops following the plugins.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }

        /// <summary>
        /// Returns the key a source is filed under: <see cref="IdentitySource.Local"/> for the
        /// internal source, the normalized key otherwise.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The key.</returns>
        private static string KeyOf(string key)
        {
            return IdentitySource.Normalize(key) ?? IdentitySource.Local;
        }
    }
}
