using KleeneStar.Core.WebRestApi;
using System.Linq;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Provides unit tests for <see cref="TableLayout"/>, the column layout a user keeps per
    /// table and a saved search keeps for its results.
    /// </summary>
    public class UnitTestTableLayout
    {
        /// <summary>
        /// The columns of a table as it defines them.
        /// </summary>
        private static RestApiTableColumn[] Defaults() =>
        [
            new() { Id = "key", Label = "Key", Visible = true },
            new() { Id = "summary", Label = "Summary", Visible = true },
            new() { Id = "workspace", Label = "Workspace", Visible = true },
            new() { Id = "description", Label = "Description", Visible = false }
        ];

        /// <summary>
        /// Verifies that a layout written and read back puts the columns in its order with its
        /// visibility and width, while the labels stay the table's own.
        /// </summary>
        [Fact]
        public void RoundTripKeepsOrderVisibilityAndWidth()
        {
            var chosen = new RestApiTableColumn[]
            {
                new() { Id = "summary", Visible = true, Width = 300 },
                new() { Id = "description", Visible = true },
                new() { Id = "key", Visible = false },
                new() { Id = "workspace", Visible = true }
            };

            var applied = TableLayout.Apply(TableLayout.Parse(TableLayout.Serialize(chosen)), Defaults()).ToList();

            Assert.Equal(["summary", "description", "key", "workspace"], applied.Select(x => x.Id));
            Assert.Equal([true, true, false, true], applied.Select(x => x.Visible));
            Assert.Equal(300u, applied[0].Width);
            Assert.Equal("Summary", applied[0].Label);
        }

        /// <summary>
        /// Verifies that a column the layout does not know follows at the tail in its default
        /// state, and an entry naming a column the table no longer has is dropped.
        /// </summary>
        [Fact]
        public void UnknownColumnsAreAppendedAndStaleEntriesDropped()
        {
            var stored = TableLayout.Parse("[{\"id\":\"workspace\"},{\"id\":\"gone\",\"visible\":true},{\"id\":\"key\",\"visible\":false}]");

            var applied = TableLayout.Apply(stored, Defaults()).ToList();

            Assert.Equal(["workspace", "key", "summary", "description"], applied.Select(x => x.Id));
            Assert.False(applied[1].Visible);
            Assert.False(applied[3].Visible);
        }

        /// <summary>
        /// Verifies that nothing usable - no layout, an empty one or one that does not parse -
        /// leaves the table's own columns as they are.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[]")]
        [InlineData("not json")]
        public void NoLayoutKeepsTheDefaults(string json)
        {
            var applied = TableLayout.Apply(TableLayout.Parse(json), Defaults()).ToList();

            Assert.Equal(["key", "summary", "workspace", "description"], applied.Select(x => x.Id));
        }
    }
}
