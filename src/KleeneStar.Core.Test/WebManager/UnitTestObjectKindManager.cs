using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the <see cref="KleeneStar.Core.WebManager.ObjectKindManager"/> — the extensible
    /// registry behind the object kinds (subtypes).
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectKindManager
    {
        /// <summary>
        /// A minimal add-on style kind used to exercise the registration path. The
        /// overview URI stays null — resolving it would require a running host.
        /// </summary>
        /// <param name="key">The kind key to register under.</param>
        private sealed class TestKind(string key) : IObjectKind
        {
            /// <summary>
            /// Gets the persisted kind key.
            /// </summary>
            public string Key => key;

            /// <summary>
            /// Gets the internationalization key of the display name.
            /// </summary>
            public string Label => $"test:{key}.label";

            /// <summary>
            /// Gets the icon representing the kind (none for the test double).
            /// </summary>
            public IIcon Icon => null!;

            /// <summary>
            /// Gets the display order (after the built-in kinds).
            /// </summary>
            public int Order => 100;

            /// <summary>
            /// Gets the overview route (none for the test double).
            /// </summary>
            public IUri OverviewUri => null!;

            /// <summary>
            /// Returns the detail route (none for the test double).
            /// </summary>
            /// <param name="objectKey">The object key (unused).</param>
            /// <returns>Always <see langword="null"/>.</returns>
            public IUri DetailUri(string objectKey) => null!;

            /// <summary>
            /// Returns the edit route (none for the test double).
            /// </summary>
            /// <param name="objectKey">The object key (unused).</param>
            /// <returns>Always <see langword="null"/>.</returns>
            public IUri EditUri(string objectKey) => null!;
        }

        /// <summary>
        /// A kind double that records the object key it was asked to build a route for,
        /// so a test can assert that the catalog dispatched to the right descriptor
        /// without needing a running host to produce a real URI.
        /// </summary>
        /// <param name="key">The kind key to register under.</param>
        private sealed class RecordingKind(string key) : IObjectKind
        {
            /// <summary>Gets the last object key passed to <see cref="DetailUri"/>.</summary>
            public string LastDetailKey { get; private set; }

            /// <summary>Gets the last object key passed to <see cref="EditUri"/>.</summary>
            public string LastEditKey { get; private set; }

            /// <summary>Gets the persisted kind key.</summary>
            public string Key => key;

            /// <summary>Gets the internationalization key of the display name.</summary>
            public string Label => $"test:{key}.label";

            /// <summary>Gets the icon representing the kind (none for the test double).</summary>
            public IIcon Icon => null!;

            /// <summary>Gets the display order (after the built-in kinds).</summary>
            public int Order => 200;

            /// <summary>Gets the overview route (none for the test double).</summary>
            public IUri OverviewUri => null!;

            /// <summary>
            /// Records the object key and returns null (a real URI needs a running host).
            /// </summary>
            /// <param name="objectKey">The object key.</param>
            /// <returns>Always <see langword="null"/>.</returns>
            public IUri DetailUri(string objectKey)
            {
                LastDetailKey = objectKey;
                return null;
            }

            /// <summary>
            /// Records the object key and returns null (a real URI needs a running host).
            /// </summary>
            /// <param name="objectKey">The object key.</param>
            /// <returns>Always <see langword="null"/>.</returns>
            public IUri EditUri(string objectKey)
            {
                LastEditKey = objectKey;
                return null;
            }
        }

        /// <summary>
        /// Verifies that the built-in kinds are registered with their persisted keys and
        /// appear in their declared order.
        /// </summary>
        [Fact]
        public void Kinds_ContainBuiltInsInOrder()
        {
            var keys = CoreHub.ObjectKindManager.Kinds.Select(k => k.Key).ToList();

            var document = keys.IndexOf(ObjectKind.Document);
            var blog = keys.IndexOf(ObjectKind.Blog);
            var issue = keys.IndexOf(ObjectKind.Issue);
            var asset = keys.IndexOf(ObjectKind.Asset);

            Assert.True(document >= 0, "expected the document kind to be registered");
            Assert.True(blog >= 0, "expected the blog kind to be registered");
            Assert.True(issue >= 0, "expected the issue kind to be registered");
            Assert.True(asset >= 0, "expected the asset kind to be registered");
            Assert.True(document < blog && blog < issue && issue < asset, "expected the built-in order documents, blogs, issues, assets");
        }

        /// <summary>
        /// Verifies the lookup normalization: keys resolve case-insensitively, null and
        /// whitespace fall back to the default kind, and unknown keys yield null.
        /// </summary>
        [Fact]
        public void GetKind_NormalizesAndResolves()
        {
            Assert.Equal(ObjectKind.Document, CoreHub.ObjectKindManager.GetKind(" Document ")?.Key);
            Assert.Equal(ObjectKind.Default, CoreHub.ObjectKindManager.GetKind(null)?.Key);
            Assert.Equal(ObjectKind.Default, CoreHub.ObjectKindManager.GetKind("   ")?.Key);
            Assert.Null(CoreHub.ObjectKindManager.GetKind("kind-of-an-uninstalled-addon"));
        }

        /// <summary>
        /// Verifies the add-on extension path: a registered custom kind becomes
        /// resolvable and appears in the kind listing, and re-registering the same key
        /// replaces the descriptor instead of duplicating it.
        /// </summary>
        [Fact]
        public void Register_AddsAndReplacesCustomKind()
        {
            var first = new TestKind("Meeting-Notes");
            CoreHub.ObjectKindManager.Register(first);

            // the key is normalized on registration
            var resolved = CoreHub.ObjectKindManager.GetKind("meeting-notes");
            Assert.Same(first, resolved);
            Assert.Contains(CoreHub.ObjectKindManager.Kinds, k => ReferenceEquals(k, first));

            // re-registering the (differently cased) key replaces, never duplicates
            var second = new TestKind("MEETING-NOTES");
            CoreHub.ObjectKindManager.Register(second);

            Assert.Same(second, CoreHub.ObjectKindManager.GetKind("meeting-notes"));
            Assert.Single(CoreHub.ObjectKindManager.Kinds, k => ObjectKind.Normalize(k.Key) == "meeting-notes");
        }

        /// <summary>
        /// Verifies that <see cref="CoreHub.ObjectKindManager.ResolveDetailUri(string, string)"/> and
        /// <see cref="CoreHub.ObjectKindManager.ResolveEditUri(string, string)"/> — and their object
        /// overloads — dispatch to the descriptor registered for the object's kind, passing
        /// the object's key through unchanged.
        /// </summary>
        [Fact]
        public void Resolve_DispatchesToKindDescriptor()
        {
            var recording = new RecordingKind("prose-dispatch-test");
            CoreHub.ObjectKindManager.Register(recording);

            CoreHub.ObjectKindManager.ResolveDetailUri("prose-dispatch-test", "OBJ-1");
            Assert.Equal("OBJ-1", recording.LastDetailKey);

            CoreHub.ObjectKindManager.ResolveDetailUri(new Model.Entities.Object { Kind = "prose-dispatch-test", Key = "OBJ-2" });
            Assert.Equal("OBJ-2", recording.LastDetailKey);

            CoreHub.ObjectKindManager.ResolveEditUri("prose-dispatch-test", "OBJ-3");
            Assert.Equal("OBJ-3", recording.LastEditKey);

            CoreHub.ObjectKindManager.ResolveEditUri(new Model.Entities.Object { Kind = "prose-dispatch-test", Key = "OBJ-4" });
            Assert.Equal("OBJ-4", recording.LastEditKey);
        }

        /// <summary>
        /// Verifies that resolving the route of a null object yields null instead of
        /// throwing.
        /// </summary>
        [Fact]
        public void Resolve_NullObject_ReturnsNull()
        {
            Assert.Null(CoreHub.ObjectKindManager.ResolveDetailUri((Model.Entities.Object)null));
            Assert.Null(CoreHub.ObjectKindManager.ResolveEditUri((Model.Entities.Object)null));
        }

        /// <summary>
        /// Verifies that an unknown kind key (e.g. the key of an uninstalled add-on) falls
        /// back to the issue descriptor rather than resolving to nothing. The built-in
        /// issue descriptor is temporarily replaced by a recording double and restored
        /// afterwards.
        /// </summary>
        [Fact]
        public void ResolveDetailUri_UnknownKind_FallsBackToIssue()
        {
            var recordingIssue = new RecordingKind(ObjectKind.Issue);

            try
            {
                CoreHub.ObjectKindManager.Register(recordingIssue);

                CoreHub.ObjectKindManager.ResolveDetailUri("kind-of-an-uninstalled-addon", "OBJ-9");

                Assert.Equal("OBJ-9", recordingIssue.LastDetailKey);
            }
            finally
            {
                // restore the real built-in issue descriptor for the other tests / the app
                CoreHub.ObjectKindManager.Register(new global::KleeneStar.Core.WebFragment.Object.Issues.Issue());
            }
        }

        /// <summary>
        /// Verifies that the issue kind exposes no dedicated edit route — issues are edited
        /// through a modal, not on a full page — while it does expose a detail route
        /// descriptor.
        /// </summary>
        [Fact]
        public void Issue_HasNoDedicatedEditRoute()
        {
            var issue = CoreHub.ObjectKindManager.GetKind(ObjectKind.Issue);

            Assert.NotNull(issue);
            Assert.Null(issue.EditUri("OBJ-1"));
        }
    }
}
