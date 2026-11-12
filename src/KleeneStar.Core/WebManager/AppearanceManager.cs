using KleeneStar.Core.WebTheme;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using WebExpress.WebApp.WebTheme;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebTheme;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Keeps the appearance every identity chose for itself in the identity's key/value
    /// preferences and answers it for the page being rendered.
    /// </summary>
    /// <remarks>
    /// The record is one JSON value (<see cref="IAppearanceManager.Scope"/> /
    /// <see cref="IAppearanceManager.Key"/>) read through <see cref="ISessionManager"/>, the
    /// store the table layouts use too. Every page asks for it several times - the theme, the
    /// scheme, the override stylesheet - so it is read once per request and kept beside the
    /// request; a write replaces that copy, so the request that saved sees what it saved.
    /// </remarks>
    public sealed class AppearanceManager : IAppearanceManager
    {
        private static readonly ConditionalWeakTable<IRequest, AppearancePreference> _resolved = new();

        /// <summary>
        /// The prefix of the cluster lock of one identity's record.
        /// </summary>
        public const string LockPrefix = "kleenestar.core.appearance:";

        /// <summary>
        /// How long a cluster lock is held at most; a write takes milliseconds.
        /// </summary>
        public static readonly TimeSpan LockLifetime = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long a change waits for the gate before it gives up.
        /// </summary>
        public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// One monitor per identity whose record is being changed in this process.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, object> _gates = new();

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after an identity's appearance has been written.
        /// </summary>
        public event EventHandler<AppearancePreference> AppearanceChanged;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AppearanceManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the appearance an identity chose.
        /// </summary>
        /// <param name="identityId">The identity.</param>
        /// <returns>The record; an empty one when nothing was chosen or nobody is named.</returns>
        public AppearancePreference Get(Guid identityId)
        {
            if (identityId == Guid.Empty)
            {
                return new AppearancePreference();
            }

            var stored = CoreHub.SessionManager.GetValue(identityId, IAppearanceManager.Scope, IAppearanceManager.Key);

            if (string.IsNullOrWhiteSpace(stored))
            {
                return new AppearancePreference(identityId);
            }

            try
            {
                var preference = JsonSerializer.Deserialize<AppearancePreference>(stored, _json)
                    ?? new AppearancePreference(identityId);

                // the owner is the row's, whatever the value says
                preference.Id = identityId;

                return Normalize(preference);
            }
            catch (JsonException ex)
            {
                // a value that no longer reads is no choice; the next save overwrites it
                _httpServerContext?.Log?.Exception(ex);

                return new AppearancePreference(identityId);
            }
        }

        /// <summary>
        /// Returns the appearance of the caller of a request, read once per request.
        /// </summary>
        /// <param name="request">The request, or <see langword="null"/> for the ambient one.</param>
        /// <returns>The record; an empty one for a caller who is not signed in.</returns>
        public AppearancePreference GetCurrent(IRequest request)
        {
            request ??= WebEx.CurrentRequest;

            if (request is null)
            {
                return Get(CoreHub.SessionManager.GetCurrentIdentityId(null));
            }

            return _resolved.GetValue(request, x => Get(CoreHub.SessionManager.GetCurrentIdentityId(x)));
        }

        /// <summary>
        /// Writes an identity's appearance; a record without any choice removes the stored one.
        /// </summary>
        /// <param name="identityId">The owner; <see cref="Guid.Empty"/> stores nothing.</param>
        /// <param name="preference">The complete record.</param>
        public void Save(Guid identityId, AppearancePreference preference)
        {
            if (identityId == Guid.Empty)
            {
                return;
            }

            AppearancePreference record;

            using (Enter(identityId))
            {
                record = Write(identityId, preference);
            }

            AppearanceChanged?.Invoke(this, record);
        }

        /// <summary>
        /// Writes a complete record; the caller holds the identity's gate.
        /// </summary>
        /// <param name="identityId">The owner.</param>
        /// <param name="preference">The complete record.</param>
        /// <returns>The record as stored (normalized).</returns>
        private static AppearancePreference Write(Guid identityId, AppearancePreference preference)
        {
            var record = Normalize(preference?.Copy() ?? new AppearancePreference());
            record.Id = identityId;

            CoreHub.SessionManager.SetValue
            (
                identityId,
                IAppearanceManager.Scope,
                IAppearanceManager.Key,
                record.IsEmpty ? null : JsonSerializer.Serialize(record, _json)
            );

            return record;
        }

        /// <summary>
        /// Enters the gate of one identity's record: a monitor per identity within the process
        /// and, when the installation is clustered, a cluster lock across the replicas - the
        /// pattern of <see cref="HierarchyLock"/>.
        /// </summary>
        /// <remarks>
        /// The record is one value, so every change is read, merge, write. Two requests of the
        /// same person - the theme selector and the dark mode switch, a second tab - that read
        /// the same state and each wrote the whole record back used to lose the change the other
        /// made; inside the gate the second one reads what the first one wrote.
        /// </remarks>
        /// <param name="identityId">The owner of the record.</param>
        /// <returns>The handle that leaves the gate.</returns>
        private static IDisposable Enter(Guid identityId)
        {
            var gate = _gates.GetOrAdd(identityId, _ => new object());

            if (!Monitor.TryEnter(gate, LockTimeout))
            {
                throw new TimeoutException("Another change of this appearance is in progress; try again.");
            }

            IDisposable cluster = null;

            try
            {
                var clusterManager = CoreHub.ComponentHub?.ClusterManager;

                if (clusterManager?.IsClustered == true)
                {
                    cluster = clusterManager.Lock(LockPrefix + identityId.ToString("N"), LockLifetime, LockTimeout)
                        ?? throw new TimeoutException("Another change of this appearance is in progress; try again.");
                }
            }
            catch
            {
                Monitor.Exit(gate);

                throw;
            }

            return new Release(() =>
            {
                try
                {
                    cluster?.Dispose();
                }
                finally
                {
                    Monitor.Exit(gate);
                }
            });
        }

        /// <summary>
        /// Runs an action once, on dispose.
        /// </summary>
        /// <param name="action">The action.</param>
        private sealed class Release(Action action) : IDisposable
        {
            private Action _action = action;

            /// <summary>
            /// Runs the action, once.
            /// </summary>
            public void Dispose()
            {
                Interlocked.Exchange(ref _action, null)?.Invoke();
            }
        }

        /// <summary>
        /// Changes the appearance of the caller of a request and keeps every other choice.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="change">The change applied to a copy of the current record.</param>
        /// <returns>Whether a signed-in caller was found to store it for.</returns>
        public bool Update(IRequest request, Action<AppearancePreference> change)
        {
            request ??= WebEx.CurrentRequest;

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return false;
            }

            AppearancePreference stored;

            using (Enter(identityId))
            {
                // the base is read from the store inside the gate - never the copy cached beside
                // the request, which another request may have overtaken since it was read
                var record = Get(identityId);
                change?.Invoke(record);

                stored = Write(identityId, record);
            }

            if (request is not null)
            {
                // the rest of this request renders what it just stored
                _resolved.AddOrUpdate(request, stored.Copy());
            }

            AppearanceChanged?.Invoke(this, stored);

            return true;
        }

        /// <summary>
        /// Resolves the theme the caller picked, provided it is registered for the application
        /// being rendered.
        /// </summary>
        /// <param name="renderContext">The request and application being rendered.</param>
        /// <returns>The theme, or <see langword="null"/>.</returns>
        public IThemeContext ResolveTheme(IRenderContext renderContext)
        {
            var themeId = GetCurrent(renderContext?.Request).Theme;
            var application = renderContext?.PageContext?.ApplicationContext ?? renderContext?.Request?.ApplicationContext;

            if (string.IsNullOrWhiteSpace(themeId) || application is null)
            {
                return null;
            }

            return _componentHub?.ThemeManager?.Themes?.FirstOrDefault(x =>
                x.ApplicationContext == application
                && string.Equals(x.ThemeId?.ToString(), themeId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tells whether a theme id names a theme registered for an application.
        /// </summary>
        /// <param name="themeId">The theme id.</param>
        /// <param name="request">The request whose application is asked.</param>
        /// <returns>Whether the theme can be used there.</returns>
        public bool IsRegistered(string themeId, IRequest request)
        {
            var application = request?.ApplicationContext;

            return !string.IsNullOrWhiteSpace(themeId)
                && application is not null
                && (_componentHub?.ThemeManager?.Themes?.Any(x =>
                    x.ApplicationContext == application
                    && string.Equals(x.ThemeId?.ToString(), themeId, StringComparison.OrdinalIgnoreCase)) ?? false);
        }

        /// <summary>
        /// Reduces every value of a record to one a stylesheet may carry.
        /// </summary>
        /// <param name="preference">The record, changed in place.</param>
        /// <returns>The same record.</returns>
        internal static AppearancePreference Normalize(AppearancePreference preference)
        {
            preference.Theme = string.IsNullOrWhiteSpace(preference.Theme) ? null : preference.Theme.Trim().ToLowerInvariant();
            preference.ColorScheme = ColorScheme.Normalize(preference.ColorScheme);
            preference.AccentColor = AppearanceOptions.NormalizeAccent(preference.AccentColor);
            preference.FontScale = preference.FontScale.HasValue
                ? AppearanceOptions.NormalizeFontScale(preference.FontScale.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : null;
            preference.Density = AppearanceOptions.NormalizeDensity(preference.Density);

            return preference;
        }

        /// <summary>
        /// Releases the resources of the manager. It holds none.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
