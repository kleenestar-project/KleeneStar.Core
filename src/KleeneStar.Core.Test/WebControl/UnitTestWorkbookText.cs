using KleeneStar.Core.WebControl;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="WorkbookText"/> - the reader of a workbook body, which a
    /// document rendered as a spreadsheet stores in the column prose keeps its document in - and
    /// for <see cref="ProseText"/> answering such a body as words rather than as its JSON.
    /// </summary>
    public class UnitTestWorkbookText
    {
        /// <summary>
        /// A workbook with a text, a number and a formula carrying its last result.
        /// </summary>
        private const string Budget = """
            {"version":1,"sheets":[{"name":"Budget","rowCount":10,"colCount":4,"cells":{"A1":{"v":"Rent"},"B1":{"v":900},"B2":{"f":"=B1*2","r":1800}}}]}
            """;

        /// <summary>
        /// A workbook nobody put anything into.
        /// </summary>
        private const string Blank = """
            {"version":1,"sheets":[{"name":"Sheet1","rowCount":1000,"colCount":26,"cells":{}}]}
            """;

        /// <summary>
        /// An editor document, which the framework's workbook parser would read as an empty
        /// workbook.
        /// </summary>
        private const string Document = """
            {"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[{"type":"text","text":"Service desk tickets.","marks":{}}]}]}}
            """;

        /// <summary>
        /// Verifies that a workbook is told apart from an editor document and from text.
        /// </summary>
        [Fact]
        public void IsWorkbook_TellsWorkbookFromDocument()
        {
            Assert.True(WorkbookText.IsWorkbook(Budget));
            Assert.True(WorkbookText.IsWorkbook(Blank));
            Assert.False(WorkbookText.IsWorkbook(Document));
            Assert.False(WorkbookText.IsWorkbook("A seeded sentence."));
            Assert.False(WorkbookText.IsWorkbook("{ \"sheets\": broken"));
            Assert.False(WorkbookText.IsWorkbook(null));
        }

        /// <summary>
        /// Verifies that a workbook reads as the shown values of its cells, a formula by its
        /// result, and that the prose reader answers the same.
        /// </summary>
        [Fact]
        public void ToPlainText_Workbook_ReadsCells()
        {
            Assert.Equal("Rent 900 1800", ProseText.ToPlainText(Budget));
            Assert.Equal("Rent 900 1800", ProseText.ToHtml(Budget));
            Assert.Equal("Service desk tickets.", ProseText.ToPlainText(Document));
        }

        /// <summary>
        /// Verifies that a workbook without filled cells is empty and one with cells is not.
        /// </summary>
        [Fact]
        public void IsEmpty_Workbook_CountsCells()
        {
            Assert.True(ProseText.IsEmpty(Blank));
            Assert.False(ProseText.IsEmpty(Budget));
        }

        /// <summary>
        /// Verifies what the workbook dialog opens on: the workbook, or nothing for a body that
        /// is none.
        /// </summary>
        [Fact]
        public void ForEditor_AnswersWorkbooksOnly()
        {
            Assert.Equal(Budget, WorkbookText.ForEditor(Budget));
            Assert.Null(WorkbookText.ForEditor(Document));
            Assert.Null(WorkbookText.ForEditor("A seeded sentence."));
        }

        /// <summary>
        /// Verifies the write side: a workbook is stored with its formula results, blank is the
        /// empty workbook, and anything else is refused.
        /// </summary>
        [Fact]
        public void TryNormalize_KeepsResultsAndRefusesOthers()
        {
            Assert.True(WorkbookText.TryNormalize(Budget, out var stored));
            Assert.Equal("Rent 900 1800", ProseText.ToPlainText(stored));

            Assert.True(WorkbookText.TryNormalize("  ", out var blank));
            Assert.Null(blank);

            Assert.False(WorkbookText.TryNormalize(Document, out _));
            Assert.False(WorkbookText.TryNormalize("A seeded sentence.", out _));
        }
    }
}
