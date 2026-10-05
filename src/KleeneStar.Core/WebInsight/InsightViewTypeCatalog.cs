using KleeneStar.Core.WebFragment.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// The registry of insight tab types. The core registers the objects table, the dashboard,
    /// Kanban, Scrum, Gantt, the calendar and the reports; a plugin extends the set by calling
    /// <see cref="Register"/> with its own <see cref="IInsightViewType"/>, the way it registers an
    /// object kind, a renderer or an authentication source. There is deliberately no enum of tab
    /// types.
    /// </summary>
    /// <remarks>
    /// The catalog is the lookup behind the persisted <see cref="Model.Entities.InsightView.ViewType"/>
    /// key and the authority the tab endpoint asks: a template nobody registered a type for
    /// cannot be added as a tab. It does not gate what is stored - a tab keeps the key of a type
    /// whose plugin is gone; the tab control leaves it out and it returns as soon as the type is
    /// registered again.
    /// </remarks>
    public static class InsightViewTypeCatalog
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<string, IInsightViewType> _types = new(StringComparer.Ordinal);
        private static int _version;

        /// <summary>
        /// Gets the key of the type the first tab of a new insight is.
        /// </summary>
        public static string Default => Model.Entities.InsightViewTypes.Objects;

        /// <summary>
        /// Gets a number that changes whenever the set of registered types does.
        /// </summary>
        public static int Version => Volatile.Read(ref _version);

        /// <summary>
        /// Initializes the catalog with the types the core ships.
        /// </summary>
        static InsightViewTypeCatalog()
        {
            Register(new ObjectsInsightViewType());
            Register(new DashboardInsightViewType());
            Register(new KanbanInsightViewType());
            Register(new ScrumInsightViewType());
            Register(new GanttInsightViewType());
            Register(new CalendarInsightViewType());
            Register(new ReportsInsightViewType());
        }

        /// <summary>
        /// Gets the registered types, ordered by <see cref="IInsightViewType.Order"/> and then by key.
        /// </summary>
        public static IEnumerable<IInsightViewType> Types
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
        /// <param name="type">The type. Must not be null and must carry a key and a template.</param>
        public static void Register(IInsightViewType type)
        {
            ArgumentNullException.ThrowIfNull(type);

            var key = Normalize(type.Key)
                ?? throw new ArgumentException("An insight view type must carry a key.", nameof(type));

            if (type.Template is null)
            {
                throw new ArgumentException("An insight view type must name its tab template.", nameof(type));
            }

            lock (_sync)
            {
                _types[key] = type;

                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>
        /// Removes a type, so it can no longer be added as a tab. The default type cannot be
        /// removed, only replaced - it is the first tab of a new insight.
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
        public static IInsightViewType Get(string key)
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
        /// Returns the client-side id of the tab template of a type - the id the tab control
        /// knows the template by.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The template id, or <see langword="null"/> for no type.</returns>
        public static string TemplateId(IInsightViewType type)
        {
            return type is null ? null : ObjectViewTemplate.TemplateId(type.Template);
        }

        /// <summary>
        /// Returns the type whose tab template the client reported.
        /// </summary>
        /// <param name="templateId">The template id, compared without case.</param>
        /// <returns>The type, or <see langword="null"/> when no registered type uses that template.</returns>
        public static IInsightViewType FromTemplateId(string templateId)
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
    }
}
