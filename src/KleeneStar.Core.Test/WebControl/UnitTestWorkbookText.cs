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
        /// Verifies what the workbook dialog opens on: the workbook as stored, and nothing for
        /// a body that says nothing.
        /// </summary>
        [Fact]
        public void ForEditor_AnswersWorkbooksAsStored()
        {
            Assert.Equal(Budget, WorkbookText.ForEditor(Budget));
            Assert.Null(WorkbookText.ForEditor(null));
            Assert.Null(WorkbookText.ForEditor("  "));
        }

        /// <summary>
        /// Verifies that a body that is no workbook - the text of a document whose class was
        /// switched to the spreadsheet renderer - opens as its blocks in the first column
        /// rather than as an empty sheet a publish would put in its place.
        /// </summary>
        /// <param name="body">The stored description.</param>
        /// <param name="expected">The first column, rows separated by a pipe.</param>
        [Theory]
        [InlineData(Document, "Service desk tickets.")]
        [InlineData("A seeded sentence.", "A seeded sentence.")]
        [InlineData("<h1>Plan</h1><p>Rent &amp; power</p><ul><li>one</li><li>=two</li></ul>", "Plan|Rent & power|one|=two")]
        [InlineData("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td></tr></table>", "a b|c")]
        public void ForEditor_ConvertsProseIntoTheFirstColumn(string body, string expected)
        {
            // act
            var workbook = WorkbookText.Read(WorkbookText.ForEditor(body));

            // validation
            Assert.NotNull(workbook);
            var cells = workbook.Sheets[0].EnumerateCells().ToList();
            Assert.All(cells, cell => Assert.Equal(0, cell.Column));
            Assert.All(cells, cell => Assert.Null(cell.Cell.Formula));
            Assert.Equal(expected.Split('|'), cells.Select(cell => (string)cell.Cell.Value));
        }

        /// <summary>
        /// Verifies that a converted text survives the round trip the dialog makes - autosave
        /// and publish normalize what it posts back - with every block in place.
        /// </summary>
        [Fact]
        public void ForEditor_ConvertedProse_SurvivesNormalization()
        {
            // act
            var accepted = WorkbookText.TryNormalize(WorkbookText.ForEditor(Document), out var stored);

            // validation
            Assert.True(accepted);
            Assert.Equal("Service desk tickets.", ProseText.ToPlainText(stored));
        }

        /// <summary>
        /// Verifies that submitted bodies that are no valid workbook are refused rather than
        /// stored: too long, nested beyond the reader's depth, no object, a newer format, or
        /// a document of the prose editor.
        /// </summary>
        /// <param name="kind">The kind of malformed body.</param>
        [Theory]
        [InlineData("oversized")]
        [InlineData("deep")]
        [InlineData("array")]
        [InlineData("version")]
        [InlineData("truncated")]
        public void TryNormalize_RefusesMalformedBodies(string kind)
        {
            // arrange
            var value = kind switch
            {
                "oversized" => "{\"sheets\":[{\"name\":\"S\",\"cells\":{\"A1\":{\"v\":\"" + new string('x', WorkbookText.MaxLength) + "\"}}}]}",
                "deep" => "{\"sheets\":[" + string.Concat(Enumerable.Repeat("[", 100)) + string.Concat(Enumerable.Repeat("]", 100)) + "]}",
                "array" => "[{\"sheets\":[]}]",
                "version" => "{\"version\":99,\"sheets\":[]}",
                _ => "{\"sheets\":[{\"name\":\"S\",\"cells\":{\"A1\":"
            };

            // act
            var accepted = WorkbookText.TryNormalize(value, out var stored);

            // validation
            Assert.False(accepted);
            Assert.Null(stored);
        }

        /// <summary>
        /// Verifies that what a client cannot be trusted with is dropped on the way in: a
        /// formula that is none, a formula beyond the length the engine reads, a cell outside
        /// the grid and a sheet dimension beyond the format's.
        /// </summary>
        [Fact]
        public void TryNormalize_SanitizesCells()
        {
            // arrange
            var value = """
                {"version":1,"sheets":[{"name":"S","rowCount":99999999,"colCount":99999999,"cells":{
                "A1":{"f":"SUM(B1)","r":5},
                "A2":{"f":"=FORMULA_TOO_LONG","r":1},
                "A3":{"f":"=A1+1","r":6},
                "XFE1":{"v":"outside"}}}]}
                """.Replace("FORMULA_TOO_LONG", new string('1', 9000));

            // act
            var accepted = WorkbookText.TryNormalize(value, out var stored);
            var sheet = WorkbookText.Read(stored).Sheets[0];

            // validation
            Assert.True(accepted);
            Assert.Null(sheet.GetCell(0, 0)?.Formula);
            Assert.Null(sheet.GetCell(1, 0)?.Formula);
            Assert.Equal("=A1+1", sheet.GetCell(2, 0).Formula);
            Assert.DoesNotContain("outside", stored);
            Assert.True(sheet.RowCount <= WebExpress.WebUI.WebSpreadsheet.SpreadsheetAddress.MaxRows);
            Assert.True(sheet.ColumnCount <= WebExpress.WebUI.WebSpreadsheet.SpreadsheetAddress.MaxColumns);
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
            Assert.True(WorkbookText.Read(blank)?.IsEmpty);

            Assert.True(WorkbookText.TryNormalize(null, out var unchanged));
            Assert.Null(unchanged);

            Assert.False(WorkbookText.TryNormalize(Document, out _));
            Assert.False(WorkbookText.TryNormalize("A seeded sentence.", out _));
        }

        /// <summary>
        /// Verifies that accepted blank bodies remain explicit empty workbooks in persistence.
        /// </summary>
        /// <param name="value">The submitted blank body.</param>
        [Theory]
        [InlineData("")]
        [InlineData(" \r\n\t")]
        public void TryNormalize_BlankBody_ProducesEmptyWorkbook(string value)
        {
            // arrange
            string stored;

            // act
            var accepted = WorkbookText.TryNormalize(value, out stored);

            // validation
            Assert.True(accepted);
            Assert.NotNull(stored);
            Assert.True(WorkbookText.Read(stored)?.IsEmpty);
        }

        /// <summary>
        /// Verifies that autosave and publication keep the rows, columns and sheets the author
        /// hid, so a reopened or published workbook does not reveal them.
        /// </summary>
        [Fact]
        public void TryNormalize_PreservesHiddenRowsColumnsAndSheets()
        {
            // arrange
            const string value = """
                {"version":1,"sheets":[{"name":"Budget","rowCount":10,"colCount":4,"cells":{"A1":{"v":"Visible"},"B1":{"v":900}},"rows":{"0":{"hidden":true}},"cols":{"1":{"hidden":true}},"props":{"hidden":true}}]}
                """;

            // act
            var accepted = WorkbookText.TryNormalize(value, out var stored);
            var sheet = System.Text.Json.Nodes.JsonNode.Parse(stored)["sheets"][0];

            // validation
            Assert.True(accepted);
            Assert.True((bool)sheet["rows"]["0"]["hidden"]);
            Assert.True((bool)sheet["cols"]["1"]["hidden"]);
            Assert.True((bool)sheet["props"]["hidden"]);
            Assert.Equal("Visible", (string)sheet["cells"]["A1"]["v"]);
            Assert.Equal(900, (int)sheet["cells"]["B1"]["v"]);
            Assert.Equal(stored, WorkbookText.TryNormalize(stored, out var again) ? again : null);
        }
    }
}
