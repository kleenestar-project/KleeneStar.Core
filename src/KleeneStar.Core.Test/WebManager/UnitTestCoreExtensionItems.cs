using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebIdentity;
using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebReport;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests that the core's kinds, renderers, authentication sources and insight tab types are
    /// findable: the managers collect them through WebExpress's plugin type catalog, which finds
    /// only public sealed types with a public parameterless constructor - an item that is not one
    /// is silently missing in a running host.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCoreExtensionItems
    {
        /// <summary>
        /// Every item the core ships follows the convention.
        /// </summary>
        [Fact]
        public void EveryShippedItemIsFindable()
        {
            var contracts = new[] { typeof(IObjectKind), typeof(IObjectRenderer), typeof(IAuthenticationSource), typeof(IInsightViewType), typeof(IObjectExportFormat) };
            var items = typeof(CoreHub).Assembly.GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && contracts.Any(c => c.IsAssignableFrom(x)))
                // a source per configuration entry is registered, not found
                .Where(x => x != typeof(OpenIdConnectAuthenticationSource))
                .ToList();

            Assert.Equal(18, items.Count);
            Assert.All(items, x =>
            {
                Assert.True(x.IsPublic && x.IsSealed, $"{x.FullName} must be public sealed, or no manager finds it.");
                Assert.NotNull(x.GetConstructor(Type.EmptyTypes));
            });
        }

        /// <summary>
        /// The managers know the core's own items without a host.
        /// </summary>
        [Fact]
        public void Managers_KnowTheCoreItems()
        {
            var kinds = CoreHub.ObjectKindManager.Kinds.Select(x => x.Key.ToLowerInvariant()).ToList();
            var renderers = CoreHub.ObjectRendererManager.Renderers.Select(x => x.Key.ToLowerInvariant()).ToList();

            Assert.Subset(kinds.ToHashSet(), new HashSet<string> { "document", "blog", "issue", "asset" });
            Assert.Subset(renderers.ToHashSet(), new HashSet<string> { "prose", "form", "spreadsheet" });
            Assert.IsType<LocalAuthenticationSource>(CoreHub.AuthenticationSourceManager.GetSource(null));
            Assert.Equal(7, CoreHub.InsightViewTypeManager.Types.Count(x => x.GetType().Assembly == typeof(CoreHub).Assembly));
            Assert.NotNull(CoreHub.InsightViewTypeManager.Get(global::KleeneStar.Core.WebManager.InsightViewTypeManager.Default));
            Assert.Subset(CoreHub.ObjectExportManager.Formats.Select(x => x.Key).ToHashSet(), new HashSet<string> { "pdf", "json", "csv" });
        }
    }
}
