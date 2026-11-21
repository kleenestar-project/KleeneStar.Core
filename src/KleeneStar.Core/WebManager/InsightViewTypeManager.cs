using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebInsight;
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
    /// The tab types of the insights, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b> <see cref="IInsightViewType"/>
    /// with a public parameterless constructor and a template - the core's objects, dashboard,
    /// Kanban, Scrum, Gantt, calendar and reports among them. There is no enum of tab types.
    /// Formerly the static <c>InsightViewTypeCatalog</c>.
    /// </summary>
    public sealed class InsightViewTypeManager : IInsightViewTypeManager
    {
        private readonly PluginTypeCatalog<IInsightViewType> _catalog = new(x => x.Template is null ? null : Normalize(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private InsightViewTypeManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                // without a plugin manager (a test, a call before the host is up) the manager
                // knows its own assembly's types
                _catalog.Collect(typeof(InsightViewTypeManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host, for <see cref="CoreHub"/> before or without one.
        /// </summary>
        /// <returns>A manager knowing the core's types.</returns>
        internal static InsightViewTypeManager CreateDetached()
        {
            return new InsightViewTypeManager(null, null);
        }

        /// <summary>
        /// Gets the key of the type the first tab of a new insight is.
        /// </summary>
        public static string Default => Model.Entities.InsightViewTypes.Objects;

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IInsightViewType> Types => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IInsightViewType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            if (Normalize(type.Key) is null)
            {
                throw new ArgumentException("An insight view type must carry a key.", nameof(type));
            }

            if (type.Template is null)
            {
                throw new ArgumentException("An insight view type must name its tab template.", nameof(type));
            }

            _catalog.Register(type);
        }

        /// <inheritdoc/>
        public bool Unregister(string key)
        {
            var normalized = Normalize(key);

            return normalized is not null && normalized != Default && _catalog.Unregister(normalized);
        }

        /// <inheritdoc/>
        public IInsightViewType Get(string key)
        {
            var normalized = Normalize(key);

            return normalized is null ? null : _catalog.Find(normalized);
        }

        /// <inheritdoc/>
        public bool IsRegistered(string key)
        {
            return Get(key) is not null;
        }

        /// <summary>
        /// Returns the client-side id of the tab template of a type - the id the tab control
        /// knows the template by.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The template id, or <see langword="null"/> for no type.</returns>
        public static string TemplateId(IInsightViewType type)
        {
            return type is null ? null : ObjectViewTemplate.TemplateId(type.Template);
        }

        /// <inheritdoc/>
        public IInsightViewType FromTemplateId(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return null;
            }

            return Types.FirstOrDefault(x => string.Equals(TemplateId(x), templateId.Trim(), StringComparison.OrdinalIgnoreCase));
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

        /// <summary>
        /// Stops following the plugins.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }
    }
}
