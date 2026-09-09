using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Provides unit tests for the <see cref="ObjectRendererCatalog"/> — the extensible
    /// registry behind the renderers, and the resolution of the renderer a class is read
    /// and written through.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectRendererCatalog
    {
        /// <summary>
        /// A minimal add-on style renderer used to exercise the registration path.
        /// </summary>
        /// <param name="key">The renderer key to register under.</param>
        /// <param name="kinds">The kinds it serves; empty means every kind.</param>
        private sealed class TestRenderer(string key, params string[] kinds) : IObjectRenderer
        {
            /// <summary>Gets the persisted renderer key.</summary>
            public string Key => key;

            /// <summary>Gets the internationalization key of the display name.</summary>
            public string Label => $"test:{key}.label";

            /// <summary>Gets the internationalization key of the description.</summary>
            public string Description => $"test:{key}.description";

            /// <summary>Gets the icon (none for the test double).</summary>
            public IIcon Icon => null!;

            /// <summary>Gets the display order (after the built-in renderers).</summary>
            public int Order => 100;

            /// <summary>Gets the kinds the renderer serves.</summary>
            public IEnumerable<string> Kinds => kinds;
        }

        /// <summary>
        /// Verifies that the built-in renderers are registered with their persisted keys
        /// and appear in their declared order.
        /// </summary>
        [Fact]
        public void Renderers_ContainBuiltInsInOrder()
        {
            var keys = ObjectRendererCatalog.Renderers.Select(x => x.Key).ToList();

            var prose = keys.IndexOf(ObjectRenderer.Prose);
            var form = keys.IndexOf(ObjectRenderer.Form);

            Assert.True(prose >= 0, "expected the prose renderer to be registered");
            Assert.True(form >= 0, "expected the form renderer to be registered");
            Assert.True(prose < form, "expected the built-in order prose, form");
        }

        /// <summary>
        /// Verifies the lookup normalization: keys resolve case-insensitively, and an unset
        /// or unknown key yields null rather than a default — "no renderer" is its own
        /// state here, unlike with the kinds.
        /// </summary>
        [Fact]
        public void GetRenderer_NormalizesAndResolves()
        {
            Assert.Equal(ObjectRenderer.Prose, ObjectRendererCatalog.GetRenderer(" Prose ")?.Key);
            Assert.Null(ObjectRendererCatalog.GetRenderer(null));
            Assert.Null(ObjectRendererCatalog.GetRenderer("   "));
            Assert.Null(ObjectRendererCatalog.GetRenderer("renderer-of-an-uninstalled-addon"));
        }

        /// <summary>
        /// Verifies which renderers a kind offers: prose declares the two kinds that have a
        /// body to write, the form declares none and therefore serves every kind — including
        /// one that did not exist when it was registered.
        /// </summary>
        [Fact]
        public void GetRenderers_FilterByKind()
        {
            var document = ObjectRendererCatalog.GetRenderers(ObjectKind.Document).Select(x => x.Key).ToList();
            var issue = ObjectRendererCatalog.GetRenderers(ObjectKind.Issue).Select(x => x.Key).ToList();

            Assert.Contains(ObjectRenderer.Prose, document);
            Assert.Contains(ObjectRenderer.Form, document);

            Assert.DoesNotContain(ObjectRenderer.Prose, issue);
            Assert.Contains(ObjectRenderer.Form, issue);

            // a kind nothing was registered against still gets the universal renderer
            var addon = ObjectRendererCatalog.GetRenderers("kind-of-an-uninstalled-addon").Select(x => x.Key).ToList();

            Assert.Contains(ObjectRenderer.Form, addon);
            Assert.DoesNotContain(ObjectRenderer.Prose, addon);
        }

        /// <summary>
        /// Verifies the gate the class endpoint validates on: an unset renderer is always
        /// acceptable, one the kind offers is acceptable, and one it does not is refused.
        /// </summary>
        [Fact]
        public void IsOffered_AcceptsUnsetAndMatching()
        {
            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, null));
            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, "  "));
            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, ObjectRenderer.Form));
            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Document, ObjectRenderer.Prose));

            Assert.False(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, ObjectRenderer.Prose));
            Assert.False(ObjectRendererCatalog.IsOffered(ObjectKind.Document, "renderer-of-an-uninstalled-addon"));
        }

        /// <summary>
        /// Verifies that a class naming no renderer follows the default of its kind: the two
        /// kinds with a body default to prose, the record-shaped ones to the mask.
        /// </summary>
        [Fact]
        public void ResolveKey_UnsetFollowsTheKind()
        {
            Assert.Equal(ObjectRenderer.Prose, ObjectRendererCatalog.ResolveKey(new Class { Kind = ObjectKind.Document }));
            Assert.Equal(ObjectRenderer.Prose, ObjectRendererCatalog.ResolveKey(new Class { Kind = ObjectKind.Blog }));
            Assert.Equal(ObjectRenderer.Form, ObjectRendererCatalog.ResolveKey(new Class { Kind = ObjectKind.Issue }));
            Assert.Equal(ObjectRenderer.Form, ObjectRendererCatalog.ResolveKey(new Class { Kind = ObjectKind.Asset }));
        }

        /// <summary>
        /// Verifies that a named renderer wins over the kind default, and that a key whose
        /// add-on is no longer installed falls back to the kind rather than resolving to
        /// nothing — the class keeps the key in the data meanwhile.
        /// </summary>
        [Fact]
        public void ResolveKey_NamedWinsAndUnknownFallsBack()
        {
            var configured = new Class { Kind = ObjectKind.Document, Renderer = ObjectRenderer.Form };

            Assert.Equal(ObjectRenderer.Form, ObjectRendererCatalog.ResolveKey(configured));
            Assert.True(ObjectRendererCatalog.IsRenderedAs(configured, ObjectRenderer.Form));
            Assert.False(ObjectRendererCatalog.IsRenderedAs(configured, ObjectRenderer.Prose));

            var orphaned = new Class { Kind = ObjectKind.Document, Renderer = "renderer-of-an-uninstalled-addon" };

            Assert.Equal(ObjectRenderer.Prose, ObjectRendererCatalog.ResolveKey(orphaned));

            // a null class renders as nothing rather than throwing
            Assert.False(ObjectRendererCatalog.IsRenderedAs(null, ObjectRenderer.Form));
        }

        /// <summary>
        /// Verifies the add-on extension path: a registered custom renderer becomes
        /// resolvable, is offered to the kinds it declares and to no others, and
        /// re-registering the same key replaces the descriptor instead of duplicating it.
        /// </summary>
        [Fact]
        public void Register_AddsAndReplacesCustomRenderer()
        {
            var first = new TestRenderer("Slide-Deck", ObjectKind.Document);
            ObjectRendererCatalog.Register(first);

            // the key is normalized on registration
            Assert.Same(first, ObjectRendererCatalog.GetRenderer("slide-deck"));
            Assert.Contains(ObjectRendererCatalog.GetRenderers(ObjectKind.Document), x => ReferenceEquals(x, first));
            Assert.DoesNotContain(ObjectRendererCatalog.GetRenderers(ObjectKind.Issue), x => ReferenceEquals(x, first));

            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Document, "slide-deck"));
            Assert.False(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, "slide-deck"));

            // a class may now name it, and resolving answers the key rather than the kind default
            Assert.Equal("slide-deck", ObjectRendererCatalog.ResolveKey(new Class { Kind = ObjectKind.Document, Renderer = "Slide-Deck" }));

            // re-registering the (differently cased) key replaces, never duplicates
            var second = new TestRenderer("SLIDE-DECK", ObjectKind.Document);
            ObjectRendererCatalog.Register(second);

            Assert.Same(second, ObjectRendererCatalog.GetRenderer("slide-deck"));
            Assert.Single(ObjectRendererCatalog.Renderers, x => ObjectRenderer.Normalize(x.Key) == "slide-deck");
        }

        /// <summary>
        /// Verifies that a renderer without a key is refused: the key is the whole of how a
        /// class names it, so a descriptor lacking one could never be selected. The picker's
        /// automatic token is refused as well, so a plugin cannot shadow the entry that
        /// clears the field.
        /// </summary>
        [Fact]
        public void Register_WithoutKeyOrReservedKey_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ObjectRendererCatalog.Register(null!));
            Assert.Throws<ArgumentException>(() => ObjectRendererCatalog.Register(new TestRenderer("   ")));
            Assert.Throws<ArgumentException>(() => ObjectRendererCatalog.Register(new TestRenderer(ObjectRendererCatalog.Automatic)));
            Assert.Throws<ArgumentException>(() => ObjectRendererCatalog.Register(new TestRenderer(" AUTO ")));

            // and the reserved key resolves to nothing, so it can never be an effective renderer
            Assert.Null(ObjectRendererCatalog.GetRenderer(ObjectRendererCatalog.Automatic));
        }

        /// <summary>
        /// Verifies the picker's automatic entry: its token unwraps to the unset state on the
        /// way in, is accepted by the offer gate for every kind, and resolves to the default
        /// of the kind — while a real renderer key travels through unchanged.
        /// </summary>
        [Fact]
        public void Unwrap_TurnsTheAutomaticTokenIntoUnset()
        {
            Assert.Null(ObjectRendererCatalog.Unwrap(ObjectRendererCatalog.Automatic));
            Assert.Null(ObjectRendererCatalog.Unwrap(" AUTO "));

            Assert.Equal(ObjectRenderer.Form, ObjectRendererCatalog.Unwrap(ObjectRenderer.Form));
            Assert.Equal("", ObjectRendererCatalog.Unwrap(""));
            Assert.Null(ObjectRendererCatalog.Unwrap(null));

            // the endpoint validates the unwrapped value, so the entry is acceptable on a kind
            // that offers only the mask — it is not a renderer, it is the absence of one
            Assert.True(ObjectRendererCatalog.IsOffered(ObjectKind.Issue, ObjectRendererCatalog.Unwrap(ObjectRendererCatalog.Automatic)));

            var cleared = new Class { Kind = ObjectKind.Document, Renderer = ObjectRendererCatalog.Unwrap(ObjectRendererCatalog.Automatic) };

            Assert.Equal(ObjectRenderer.Prose, ObjectRendererCatalog.ResolveKey(cleared));
        }
    }
}
