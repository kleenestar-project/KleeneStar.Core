using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The registry of the renderers known to the application. The core registers its
    /// built-in renderers (prose, form); add-ons extend the set by calling
    /// <see cref="Register"/> with their own <see cref="IObjectRenderer"/> descriptor,
    /// typically from their plugin initialization.
    /// </summary>
    /// <remarks>
    /// The catalog is the semantic lookup behind the persisted
    /// <see cref="Model.Entities.Class.Renderer"/> key, and it answers the one question
    /// every rendering surface asks: <em>is this object mine to draw?</em> - see
    /// <see cref="IsRenderedAs(Model.Entities.Class, string)"/> and the conditions built on
    /// it. Like <see cref="ObjectKindCatalog"/> it deliberately does not gate persistence:
    /// an unknown key survives in the data layer so a class outlives the add-on that gave
    /// it its renderer, and merely falls back to the default of its kind while the add-on
    /// is gone.
    /// </remarks>
    public static class ObjectRendererCatalog
    {
        /// <summary>
        /// The value the renderer picker submits for <em>follow the object type</em>.
        /// </summary>
        /// <remarks>
        /// The unset state is a <see langword="null"/> column, and the honest option value for
        /// it would be the empty string - but an option carries its value in its element id,
        /// and an empty id is dropped by the selection control on the client. So the picker
        /// sends this token instead and <see cref="Unwrap"/> turns it back into "unset" at the
        /// endpoint, before anything persists it. It is reserved: <see cref="Register"/>
        /// refuses it as a renderer key, so a plugin cannot shadow the entry that clears the
        /// field.
        /// </remarks>
        public const string Automatic = "auto";

        private static readonly object _sync = new();
        private static readonly Dictionary<string, IObjectRenderer> _renderers = new(StringComparer.OrdinalIgnoreCase);
        private static int _version;

        /// <summary>
        /// Gets a number that changes whenever the set of registered renderers does.
        /// </summary>
        /// <remarks>
        /// The renderer picker on the class dialogs is a control on a cached fragment, built
        /// once and reused for every request, so a renderer a plugin registers after that
        /// fragment was constructed would never appear in it - and a plugin registering after
        /// the core's own components is the normal case, not the exception. Rather than
        /// rebuilding the options on every render, the picker projects the catalog again only
        /// when this number has moved.
        /// </remarks>
        public static int Version => Volatile.Read(ref _version);

        /// <summary>
        /// Initializes the catalog with the built-in core renderers.
        /// </summary>
        static ObjectRendererCatalog()
        {
            Register(new Renderers.ProseRenderer());
            Register(new Renderers.FormRenderer());
        }

        /// <summary>
        /// Gets the registered renderers, ordered by <see cref="IObjectRenderer.Order"/>
        /// and then by key.
        /// </summary>
        public static IEnumerable<IObjectRenderer> Renderers
        {
            get
            {
                lock (_sync)
                {
                    return [.. _renderers.Values
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
                }
            }
        }

        /// <summary>
        /// Registers the supplied renderer descriptor. The key is normalized via
        /// <see cref="Model.Entities.ObjectRenderer.Normalize"/>; registering a key that is
        /// already present replaces the existing descriptor, so an add-on may override the
        /// presentation of a built-in renderer.
        /// </summary>
        /// <param name="renderer">The renderer descriptor to register. Must not be null,
        /// and must carry a key.</param>
        public static void Register(IObjectRenderer renderer)
        {
            ArgumentNullException.ThrowIfNull(renderer);

            var key = Model.Entities.ObjectRenderer.Normalize(renderer.Key)
                ?? throw new ArgumentException("A renderer must carry a key.", nameof(renderer));

            if (string.Equals(key, Automatic, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The renderer key '{Automatic}' is reserved for the picker's automatic entry.", nameof(renderer));
            }

            lock (_sync)
            {
                _renderers[key] = renderer;

                // the pickers watch this, so a renderer contributed by a plugin that loaded
                // after them still reaches the dialogs
                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>
        /// Turns the picker's <see cref="Automatic"/> token back into the unset state, and
        /// leaves every other value alone. Called on the way in, before anything is persisted,
        /// so the token never reaches the data layer.
        /// </summary>
        /// <param name="renderer">The submitted renderer key. May be null.</param>
        /// <returns>The key, or <see langword="null"/> when it was the automatic token.</returns>
        public static string Unwrap(string renderer)
        {
            return string.Equals(Model.Entities.ObjectRenderer.Normalize(renderer), Automatic, StringComparison.OrdinalIgnoreCase)
                ? null
                : renderer;
        }

        /// <summary>
        /// Resolves a renderer key to its registered descriptor.
        /// </summary>
        /// <param name="key">The renderer key to resolve. May be null.</param>
        /// <returns>
        /// The registered descriptor, or <see langword="null"/> when the key is unset or no
        /// renderer with it is registered (e.g. it belongs to an uninstalled add-on).
        /// </returns>
        public static IObjectRenderer GetRenderer(string key)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(key);

            if (normalized is null)
            {
                return null;
            }

            lock (_sync)
            {
                return _renderers.TryGetValue(normalized, out var renderer) ? renderer : null;
            }
        }

        /// <summary>
        /// Gets the renderers a kind offers - the ones that serve it (by declaring it, or by
        /// declaring no kind at all), narrowed to the ones the kind itself accepts.
        /// </summary>
        /// <remarks>
        /// Both sides have to agree, and both express "no opinion" as an empty collection:
        /// <see cref="IObjectRenderer.Kinds"/> is what a renderer is capable of drawing,
        /// <see cref="IObjectKind.Renderers"/> what a kind is willing to be read through. The
        /// second is what lets the blog kind refuse the mask without the mask - which serves
        /// every kind, including the ones not yet written - having to know that blogs exist.
        /// </remarks>
        /// <param name="kind">The kind key. May be null, which resolves to the default kind.</param>
        /// <returns>The renderers, in listing order.</returns>
        public static IEnumerable<IObjectRenderer> GetRenderers(string kind)
        {
            var normalized = Model.Entities.ObjectKind.Normalize(kind);
            var accepted = ObjectKindCatalog.GetKind(normalized)?.Renderers?
                .Select(Model.Entities.ObjectRenderer.Normalize)
                .Where(x => x is not null)
                .ToList() ?? [];

            return Renderers.Where(x => Serves(x, normalized) && Accepts(accepted, x));
        }

        /// <summary>
        /// Gets the keys of the registered kinds that offer the supplied renderer - the
        /// inverse of <see cref="GetRenderers(string)"/>, answered by the same two-sided
        /// handshake.
        /// </summary>
        /// <remarks>
        /// The renderer picker on the class dialogs needs the question this way round: it
        /// draws one entry per renderer and has to say, on each of them, which object types it
        /// belongs to, so the picker can narrow itself to the type chosen beside it. The
        /// answer covers only the kinds registered at the time it is asked, which is why the
        /// picker projects it again when either catalog moves.
        /// </remarks>
        /// <param name="renderer">The renderer key. May be null, which answers nothing.</param>
        /// <returns>The kind keys, in the listing order of the kinds.</returns>
        public static IEnumerable<string> GetKinds(string renderer)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(renderer);

            if (normalized is null)
            {
                return [];
            }

            return [.. ObjectKindCatalog.Kinds
                .Where(kind => GetRenderers(kind.Key)
                    .Any(x => string.Equals(Model.Entities.ObjectRenderer.Normalize(x.Key), normalized, StringComparison.OrdinalIgnoreCase)))
                .Select(kind => Model.Entities.ObjectKind.Normalize(kind.Key))];
        }

        /// <summary>
        /// Determines whether the supplied renderer key is one the kind offers. An unset
        /// key is always acceptable - it is the class saying "follow the kind".
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="renderer">The renderer key. May be null.</param>
        /// <returns>True when the key is unset, or names a renderer registered for the kind.</returns>
        public static bool IsOffered(string kind, string renderer)
        {
            var normalized = Model.Entities.ObjectRenderer.Normalize(renderer);

            return normalized is null || GetRenderers(kind)
                .Any(x => string.Equals(Model.Entities.ObjectRenderer.Normalize(x.Key), normalized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves the renderer key in effect for the supplied class: the one it names,
        /// or - when it names none, or names one no longer installed - the default of its
        /// kind.
        /// </summary>
        /// <param name="class">The class to resolve. May be null.</param>
        /// <returns>The effective renderer key. Never null.</returns>
        public static string ResolveKey(Model.Entities.Class @class)
        {
            return ResolveKey(@class?.Kind, @class?.Renderer);
        }

        /// <summary>
        /// Resolves the renderer key in effect for the supplied kind and configured
        /// renderer.
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="renderer">The configured renderer key. May be null.</param>
        /// <returns>The effective renderer key. Never null.</returns>
        public static string ResolveKey(string kind, string renderer)
        {
            var configured = Model.Entities.ObjectRenderer.Normalize(renderer);

            // a key that resolves to nothing belongs to an add-on that is no longer
            // installed; the class keeps it in the data and reads as its kind meanwhile.
            // a key the kind does not offer is read the same way - the endpoint refuses to
            // store one, but a class may have been given its renderer before the kind
            // declined it, or moved to a kind that does, and a renderer no fragment on the
            // kind's routes is gated on would leave the object with no reading view at all
            if (configured is not null && GetRenderer(configured) is not null && IsOffered(kind, configured))
            {
                return configured;
            }

            var descriptor = ObjectKindCatalog.GetKind(kind) ?? ObjectKindCatalog.GetKind(Model.Entities.ObjectKind.Default);

            return Model.Entities.ObjectRenderer.Normalize(descriptor?.DefaultRenderer)
                ?? Model.Entities.ObjectRenderer.Form;
        }

        /// <summary>
        /// Resolves the renderer descriptor in effect for the supplied class.
        /// </summary>
        /// <param name="class">The class to resolve. May be null.</param>
        /// <returns>The descriptor, or <see langword="null"/> when the effective key is not
        /// registered either.</returns>
        public static IObjectRenderer Resolve(Model.Entities.Class @class)
        {
            return GetRenderer(ResolveKey(@class));
        }

        /// <summary>
        /// Determines whether the supplied class is rendered by the renderer with the
        /// supplied key. This is the question every renderer-specific fragment asks about
        /// the object it was about to draw.
        /// </summary>
        /// <param name="class">The class to test. May be null, which answers false.</param>
        /// <param name="renderer">The renderer key to test against.</param>
        /// <returns>True when the class renders through that renderer.</returns>
        public static bool IsRenderedAs(Model.Entities.Class @class, string renderer)
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

        /// <summary>
        /// Determines whether the supplied descriptor serves the supplied (already
        /// normalized) kind key.
        /// </summary>
        /// <param name="renderer">The descriptor to test.</param>
        /// <param name="kind">The normalized kind key.</param>
        /// <returns>True when the renderer declares the kind, or declares none at all.</returns>
        private static bool Serves(IObjectRenderer renderer, string kind)
        {
            var kinds = renderer.Kinds?.ToList() ?? [];

            return kinds.Count == 0 || kinds.Any(x => string.Equals(Model.Entities.ObjectKind.Normalize(x), kind, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether a kind that named the supplied (already normalized) renderer
        /// keys accepts the supplied descriptor.
        /// </summary>
        /// <param name="accepted">The renderer keys the kind named; empty means every one.</param>
        /// <param name="renderer">The descriptor to test.</param>
        /// <returns>True when the kind named the renderer, or named none at all.</returns>
        private static bool Accepts(List<string> accepted, IObjectRenderer renderer)
        {
            return accepted.Count == 0 || accepted.Any(x => string.Equals(x, Model.Entities.ObjectRenderer.Normalize(renderer.Key), StringComparison.OrdinalIgnoreCase));
        }
    }
}
