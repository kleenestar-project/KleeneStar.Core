using KleeneStar.Core.WebControl;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="TemplateCategories"/>, the categories of a template
    /// stored separated by semicolons.
    /// </summary>
    public class UnitTestTemplateCategories
    {
        /// <summary>
        /// Verifies that a stored text is read trimmed, in order and each category once.
        /// </summary>
        [Fact]
        public void Parse_TrimsAndDropsRepeats()
        {
            Assert.Equal(["IT Support", "Hardware"], TemplateCategories.Parse(" IT Support ;Hardware;;it support"));
            Assert.Empty(TemplateCategories.Parse(null));
            Assert.Empty(TemplateCategories.Parse(" ; "));
        }

        /// <summary>
        /// Verifies that a single category, as the seeds and workspace templates write it, reads as
        /// one category.
        /// </summary>
        [Fact]
        public void Parse_ReadsASingleCategory()
        {
            Assert.Equal(["Knowledge Base"], TemplateCategories.Parse("Knowledge Base"));
        }

        /// <summary>
        /// Verifies that a submitted text is stored without blanks and repeats, and nothing as null.
        /// </summary>
        [Fact]
        public void Normalize_WritesTheStoredForm()
        {
            Assert.Equal("IT;HR", TemplateCategories.Normalize("IT; HR ;it;"));
            Assert.Null(TemplateCategories.Normalize(";"));
            Assert.Null(TemplateCategories.Normalize(null));
        }

        /// <summary>
        /// Verifies that a category is found in a stored text ignoring case, and a part of a
        /// category is not.
        /// </summary>
        [Fact]
        public void Contains_MatchesWholeCategories()
        {
            Assert.True(TemplateCategories.Contains("IT Support;HR", "hr"));
            Assert.False(TemplateCategories.Contains("IT Support;HR", "IT"));
        }
    }
}
