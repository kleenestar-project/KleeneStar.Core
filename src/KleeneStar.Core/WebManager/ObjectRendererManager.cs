using KleeneStar.Core.WebFragment.Object;
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
    /// The renderers of the installation, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b>
    /// <see cref="IObjectRenderer"/> with a public parameterless constructor of any plugin - the
    /// core's prose, form and spreadsheet renderers among them. There is no enum of renderers.
    /// Formerly the static <c>ObjectRendererCatalog</c>.
    /// </summary>
    public sealed class ObjectRendererManager : IObjectRendererManager
    {
        /// <summary>
        /// The value the renderer picker submits for <em>follow the object type</em>.
        /// </summary>
        /// <remarks>
        /// The unset state is a <see langword="null"/> column, and the honest option value for
        /// it would be the empty string - but an option carries its value in its element id, and
        /// an empty id is dropped by the selection control on the client. So the picker sends
        /// this token instead and <see cref="Unwrap"/> turns it back into "unset" at the
        /// endpoint, before anything persists it. It is reserved: no renderer is filed under it,
        /// so a plugin cannot shadow the entry that clears the field.
        /// </remarks>
        public const string Automatic = "auto";

        private readonly PluginTypeCatalog<IObjectRenderer> _catalog = new(KeyOf);

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectRendererManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                // without a plugin manager (a test, a call before the host is up) the manager
                // knows its own assembly's renderers
                _catalog.Collect(typeof(ObjectRendererManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host, for <see cref="CoreHub"/> before or without one.
        /// </summary>
        /// <returns>A manager knowing the core's renderers.</returns>
        internal static ObjectRendererManager CreateDetached()
        {
            return new ObjectRendererManager(null, null);
        }

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IObjectRenderer> Renderers => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IObjectRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);

            var key = Model.Entities.ObjectRenderer.Normalize(renderer.Key)
                ?? throw new ArgumentException("A renderer must carry a key.", nameof(renderer));

            if (string.Equals(key, Automatic, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The renderer key '{Automatic}' is reserved for the picker's automatic entry.", nameof(renderer));
            }

            _catalog.Register(renderer);
        }

        /// <summary>
        /// Turns the picker's <see cref="Automatic"/> token back into the unset state, and
        /// leaves every other value alone. Called on the way in, before anything is persisted.
        /// </summary>
        /// <param name="renderer">The submitted renderer key. May be null.</param>
        /// <returns>The key, or <see langword="null"/> when it was the automatic token.</returns>
        public static string Unwrap(string renderer)
        {
            return string.Equals(Model.Entities.ObjectRenderer.Normalize(renderer), Automatic, StringComparison.OrdinalIgnoreCase)
                ? null
                : renderer;
        }

        /// <inheritdoc/>
        public IObjectRenderer GetRenderer(string key)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(key);

            return normalized is null ? null : _catalog.Find(normalized);
        }

        /// <inheritdoc/>
        public IEnumerable<IObjectRenderer> GetRenderers(string kind)
        {
            var normalized = Model.Entities.ObjectKind.Normalize(kind);
            var accepted = CoreHub.ObjectKindManager.GetKind(normalized)?.Renderers?
                .Select(Model.Entities.ObjectRenderer.Normalize)
                .Where(x => x is not null)
                .ToList() ?? [];

            return Renderers.Where(x => Serves(x, normalized) && Accepts(accepted, x));
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetKinds(string renderer)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(renderer);

            if (normalized is null)
            {
                return [];
            }

            return [.. CoreHub.ObjectKindManager.Kinds
                .Where(kind => GetRenderers(kind.Key)
                    .Any(x => string.Equals(Model.Entities.ObjectRenderer.Normalize(x.Key), normalized, StringComparison.OrdinalIgnoreCase)))
                .Select(kind => Model.Entities.ObjectKind.Normalize(kind.Key))];
        }

        /// <inheritdoc/>
        public bool IsOffered(string kind, string renderer)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(renderer);

            return normalized is null || GetRenderers(kind)
                .Any(x => string.Equals(Model.Entities.ObjectRenderer.Normalize(x.Key), normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public string ResolveKey(Model.Entities.Class @class)
        {
            return ResolveKey(@class?.Kind, @class?.Renderer);
        }

        /// <inheritdoc/>
        public string ResolveKey(string kind, string renderer)
        {
            var configured = Model.Entities.ObjectRenderer.Normalize(renderer);

            // a key that resolves to nothing belongs to an add-on that is no longer installed;
            // the class keeps it in the data and reads as its kind meanwhile. a key the kind
            // does not offer is read the same way - the endpoint refuses to store one, but a
            // class may have been given its renderer before the kind declined it, and a renderer
            // no fragment on the kind's routes is gated on would leave the object unreadable
            if (configured is not null && GetRenderer(configured) is not null && IsOffered(kind, configured))
            {
                return configured;
            }

            var kinds = CoreHub.ObjectKindManager;
            var descriptor = kinds.GetKind(kind) ?? kinds.GetKind(Model.Entities.ObjectKind.Default);

            return Model.Entities.ObjectRenderer.Normalize(descriptor?.DefaultRenderer)
                ?? Model.Entities.ObjectRenderer.Form;
        }

        /// <inheritdoc/>
        public IObjectRenderer Resolve(Model.Entities.Class @class)
        {
            return GetRenderer(ResolveKey(@class));
        }

        /// <inheritdoc/>
        public bool IsRenderedAs(Model.Entities.Class @class, string renderer)
        {
            if (@class is null)
            {
                return false;
            }

            return string.Equals
            (
                ResolveKey(@class),
                Model.Entities.ObjectRenderer.Normalize(renderer),
                StringComparison.OrdinalIgnoreCase
            );
        }

        /// <inheritdoc/>
        public bool IsStructured(Model.Entities.Class @class)
        {
            if (@class is null)
            {
                return false;
            }

            return Resolve(@class)?.Structured ?? true;
        }

        /// <summary>
        /// Stops following the plugins.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }

        /// <summary>
        /// Returns the key a renderer is filed under; a renderer claiming the reserved
        /// <see cref="Automatic"/> token names no key and is skipped.
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <returns>The normalized key, or <see langword="null"/>.</returns>
        private static string KeyOf(IObjectRenderer renderer)
        {
            var key = Model.Entities.ObjectRenderer.Normalize(renderer.Key);

            return string.Equals(key, Automatic, StringComparison.OrdinalIgnoreCase) ? null : key;
        }

        /// <summary>
        /// Determines whether a renderer serves an (already normalized) kind key.
        /// </summary>
        /// <param name="renderer">The renderer.</param>
        /// <param name="kind">The normalized kind key.</param>
        /// <returns>True when the renderer declares the kind, or declares none at all.</returns>
        private static bool Serves(IObjectRenderer renderer, string kind)
        {
            var kinds = renderer.Kinds?.ToList() ?? [];

            return kinds.Count == 0 || kinds.Any(x => string.Equals(Model.Entities.ObjectKind.Normalize(x), kind, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether a kind that named the supplied (normalized) renderer keys accepts a renderer.
        /// </summary>
        /// <param name="accepted">The renderer keys the kind named; empty means every one.</param>
        /// <param name="renderer">The renderer.</param>
        /// <returns>True when the kind named the renderer, or named none at all.</returns>
        private static bool Accepts(List<string> accepted, IObjectRenderer renderer)
        {
            return accepted.Count == 0 || accepted.Any(x => string.Equals(x, Model.Entities.ObjectRenderer.Normalize(renderer.Key), StringComparison.OrdinalIgnoreCase));
        }
    }
}
