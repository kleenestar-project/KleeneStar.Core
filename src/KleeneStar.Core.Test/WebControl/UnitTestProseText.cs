using KleeneStar.Core.WebControl;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="ProseText"/> — the reader every card, table cell and
    /// tooltip takes a description through. Each entity dialog writes its description with the
    /// prose editor, so what is stored is the editor's versioned json document; a seeded
    /// description is the sentence itself, and both have to read as words.
    /// </summary>
    public class UnitTestProseText
    {
        /// <summary>
        /// A document with two paragraphs, as the editor stores it.
        /// </summary>
        private const string Written = """
            {"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[{"type":"text","text":"Service desk tickets.","marks":{}}]},{"type":"p","id":"n3","children":[{"type":"text","text":"One per report.","marks":{}}]}]}}
            """;

        /// <summary>
        /// The document the editor submits for a surface nobody wrote into.
        /// </summary>
        private const string Empty = """
            {"version":1,"doc":{"type":"doc","id":"n1","attrs":{},"children":[{"type":"row","id":"n4","attrs":{},"children":[{"type":"region","id":"n3","attrs":{"weight":1},"children":[{"type":"p","id":"n2","attrs":{},"children":[]}]}]}]}}
            """;

        /// <summary>
        /// A document carrying a picture and no sentence.
        /// </summary>
        private const string Illustrated = """
            {"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[{"type":"image","id":"n3","attrs":{"src":"/assets/img/banner.svg","alt":"Banner"}}]}]}}
            """;

        /// <summary>
        /// Verifies that a stored document reads as its words, with the blocks separated rather
        /// than run together.
        /// </summary>
        [Fact]
        public void ToPlainText_Document_ReadsAsWords()
        {
            Assert.Equal("Service desk tickets. One per report.", ProseText.ToPlainText(Written));
        }

        /// <summary>
        /// Verifies that text which is no document passes through untouched: a seeded
        /// description is a sentence, and reading it would only risk changing it.
        /// </summary>
        /// <param name="value">The stored description.</param>
        [Theory]
        [InlineData("Service desk tickets.")]
        [InlineData("  ")]
        [InlineData(null)]
        [InlineData("{not a document")]
        public void ToPlainText_NoDocument_PassesThrough(string value)
        {
            Assert.Equal(value, ProseText.ToPlainText(value));
        }

        /// <summary>
        /// Verifies that the markup of a document is answered for the readers that want more
        /// than the words - the blog feed illustrates a teaser with the pictures it finds in it.
        /// </summary>
        [Fact]
        public void ToHtml_Document_RendersTheMarkup()
        {
            Assert.Contains("<img", ProseText.ToHtml(Illustrated));
            Assert.Contains("/assets/img/banner.svg", ProseText.ToHtml(Illustrated));
        }

        /// <summary>
        /// Verifies that a description which is no document is answered as it stands, which is
        /// what a seeded sentence and a legacy html body already are.
        /// </summary>
        [Fact]
        public void ToHtml_NoDocument_PassesThrough()
        {
            Assert.Equal("<p>Seeded</p>", ProseText.ToHtml("<p>Seeded</p>"));
        }

        /// <summary>
        /// Verifies what counts as nothing: no value, blank text, and the document an untouched
        /// editor submits - the last of which is why the question needs asking at all, since a
        /// caller comparing it against the empty string reads it as something the user said.
        /// </summary>
        /// <param name="value">The stored description.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(Empty)]
        public void IsEmpty_SaysNothing(string value)
        {
            Assert.True(ProseText.IsEmpty(value));
        }

        /// <summary>
        /// Verifies what counts as something: words, a document with words, and a document
        /// carrying a picture and no sentence - a post that is a photograph says something.
        /// </summary>
        /// <param name="value">The stored description.</param>
        [Theory]
        [InlineData("Service desk tickets.")]
        [InlineData(Written)]
        [InlineData(Illustrated)]
        [InlineData("{not a document")]
        public void IsEmpty_SaysSomething(string value)
        {
            Assert.False(ProseText.IsEmpty(value));
        }
    }
}
