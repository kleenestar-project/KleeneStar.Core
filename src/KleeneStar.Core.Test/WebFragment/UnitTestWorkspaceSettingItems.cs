using KleeneStar.Core.WebFragment.Workspace;
using System.Reflection;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Tests the entries of the workspace cog: WebExpress finds a fragment only when it is
    /// public and sealed, so an entry that is not is silently missing from the menu; every entry
    /// names one of the cog's sections and an order.
    /// </summary>
    public class UnitTestWorkspaceSettingItems
    {
        /// <summary>
        /// Every entry the core ships is public sealed, stands in a section of the cog and has an
        /// order - and the core ships its nine (automation and integrations are entries of their plugins).
        /// </summary>
        [Fact]
        public void EveryEntryIsFoundAndPlaced()
        {
            var entries = typeof(WorkspaceSettingItemFragment).Assembly.GetTypes()
                .Where(x => typeof(WorkspaceSettingItemFragment).IsAssignableFrom(x) && !x.IsAbstract)
                .ToList();

            Assert.Equal(9, entries.Count);

            Assert.All(entries, x =>
            {
                Assert.True(x.IsPublic && x.IsSealed, $"{x.FullName} must be public sealed, or WebExpress never finds it.");

                var sections = x.GetCustomAttributes()
                    .Where(a => a.GetType().IsGenericType && a.GetType().GetGenericTypeDefinition() == typeof(SectionAttribute<>))
                    .Select(a => a.GetType().GetGenericArguments()[0])
                    .ToList();

                Assert.True(sections.Contains(typeof(SectionWorkspaceSettingPrimary)) || sections.Contains(typeof(SectionWorkspaceSettingSecondary)),
                    $"{x.FullName} must stand in a section of the workspace cog.");
                Assert.NotNull(x.GetCustomAttribute<OrderAttribute>());
            });
        }
    }
}
