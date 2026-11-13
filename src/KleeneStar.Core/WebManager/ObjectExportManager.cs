using KleeneStar.Core.WebReport;
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
    /// The file formats of the single object export, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b> <see cref="IObjectExportFormat"/>
    /// with a public parameterless constructor; the core ships PDF, JSON and CSV
    /// (<c>WebReport/Formats</c>).
    /// </summary>
    /// <remarks>
    /// A format with a blank key is skipped. The latest offer of a key is in use; a plugin that
    /// leaves takes only the keys it still provides. Without a host (tests, calls before the hub
    /// is set) <see cref="CoreHub.ObjectExportManager"/> answers from a detached manager that
    /// knows the core's formats.
    /// </remarks>
    public sealed class ObjectExportManager : IObjectExportManager
    {
        private readonly PluginTypeCatalog<IObjectExportFormat> _catalog = new(x => Normalize(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectExportManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                _catalog.Collect(typeof(ObjectExportManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host, for <see cref="CoreHub"/> before or without one.
        /// </summary>
        /// <returns>A manager knowing the core's formats.</returns>
        internal static ObjectExportManager CreateDetached()
        {
            return new ObjectExportManager(null, null);
        }

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IObjectExportFormat> Formats => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IObjectExportFormat format)
        {
            ArgumentNullException.ThrowIfNull(format);

            if (Normalize(format.Key) is null)
            {
                throw new ArgumentException("An export format must carry a key.", nameof(format));
            }

            _catalog.Register(format);
        }

        /// <inheritdoc/>
        public bool Unregister(string key)
        {
            var normalized = Normalize(key);

            return normalized is not null && _catalog.Unregister(normalized);
        }

        /// <inheritdoc/>
        public IObjectExportFormat Get(string key)
        {
            var normalized = Normalize(key);

            return normalized is null ? null : _catalog.Find(normalized);
        }

        /// <summary>
        /// Returns the key a format is filed under: trimmed and lower-cased, or
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
