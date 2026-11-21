using KleeneStar.Core.WebReport;
using KleeneStar.Core.WebReport.Formats;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebUI.WebSpreadsheet;

namespace KleeneStar.Core.Test.WebReport
{
    /// <summary>
    /// Tests the single object export: the description text of <see cref="ObjectExport"/>, file
    /// names, and the formats <see cref="JsonObjectExportFormat"/>, <see cref="CsvObjectExportFormat"/>
    /// and <see cref="PdfObjectExportFormat"/> written from one record.
    /// </summary>
    public class UnitTestObjectExport
    {
        /// <summary>
        /// Builds a record with two attributes, a description, three fields, two relations and
        /// two comments.
        /// </summary>
        /// <returns>The record.</returns>
        private static ObjectExportRecord Record()
        {
            return new ObjectExportRecord
            {
                Key = "SD-12",
                Summary = "Printer offline",
                Attributes =
                [
                    new ObjectExportAttribute("key", "Key", "SD-12", "SD-12"),
                    new ObjectExportAttribute("created", "Created", "2026-11-13T08:00:00Z", "13.11.2026 08:00")
                ],
                Description = "The printer on floor 2 is offline.",
                DescriptionText = "The printer on floor 2 is offline.",
                Fields =
                [
                    new ObjectExportField("Priority", FieldType.Priority, "High", "High"),
                    new ObjectExportField("Secret", FieldType.Password, null, "••••••••"),
                    new ObjectExportField("Notes", FieldType.Text, null, null)
                ],
                Relations =
                [
                    new ObjectExportRelation("blocks", "SD-13", "Toner order", "Request", "Active", null),
                    new ObjectExportRelation("documented in", null, "Vendor manual", null, "Active", "https://example.com/manual")
                ],
                Comments =
                [
                    new ObjectExportComment("Ada", new DateTime(2026, 11, 13, 8, 30, 0, DateTimeKind.Utc), false, false, "Restarted the spooler.", "Restarted the spooler."),
                    new ObjectExportComment("Bob", new DateTime(2026, 11, 13, 8, 45, 0, DateTimeKind.Utc), true, true, "Spooler crashes again.", "Spooler crashes again.")
                ],
                Exported = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc)
            };
        }

        /// <summary>
        /// Builds a workbook whose cell text exceeds the preview limit of
        /// <see cref="WebControl.WorkbookText"/>; the last cell carries a marker.
        /// </summary>
        /// <param name="marker">The text of the last cell.</param>
        /// <returns>The serialized workbook.</returns>
        private static string LargeWorkbook(string marker)
        {
            var workbook = new SpreadsheetWorkbook();
            var sheet = workbook.AddSheet("Budget");

            for (var row = 1; row <= 200; row++)
            {
                sheet.SetValue("A" + row, "Cost centre " + row.ToString(CultureInfo.InvariantCulture));
                sheet.SetValue("B" + row, row * 100);
            }

            sheet.SetValue("A201", marker);

            return workbook.ToJson();
        }

        /// <summary>
        /// The description text of a workbook carries every cell, including the cells behind the
        /// preview limit, which the preview reader cuts off.
        /// </summary>
        [Fact]
        public void DescriptionText_KeepsWorkbookCellsBehindThePreviewLimit()
        {
            var description = LargeWorkbook("END-OF-SHEET");

            var preview = global::KleeneStar.Core.WebControl.ProseText.ToPlainText(description);
            var text = ObjectExport.DescriptionText(description);

            Assert.True(text.Length > global::KleeneStar.Core.WebControl.WorkbookText.MaxPlainTextLength);
            Assert.DoesNotContain("END-OF-SHEET", preview);
            Assert.Contains("Cost centre 200", text);
            Assert.EndsWith("END-OF-SHEET", text);
        }

        /// <summary>
        /// The JSON and CSV export of a large workbook carry the cells behind the preview limit.
        /// </summary>
        [Fact]
        public void JsonAndCsv_KeepWorkbookCellsBehindThePreviewLimit()
        {
            var description = LargeWorkbook("END-OF-SHEET");
            var record = new ObjectExportRecord
            {
                Key = "FIN-2",
                Description = description,
                DescriptionText = ObjectExport.DescriptionText(description),
                Exported = DateTime.UtcNow
            };

            using var json = JsonDocument.Parse(JsonObjectExportFormat.ToJson(record));

            Assert.EndsWith("END-OF-SHEET", json.RootElement.GetProperty("description").GetString());
            Assert.Contains("END-OF-SHEET", CsvObjectExportFormat.ToCsv(record, ';').ToString());
        }

        /// <summary>
        /// A text that is no workbook is read as prose.
        /// </summary>
        [Fact]
        public void DescriptionText_ReadsProse()
        {
            Assert.Equal("Plain words.", ObjectExport.DescriptionText("Plain words."));
            Assert.Equal(string.Empty, ObjectExport.DescriptionText(null) ?? string.Empty);
        }

        /// <summary>
        /// The file is named after the object key; characters a file name cannot carry are replaced.
        /// </summary>
        [Fact]
        public void FileName_UsesTheKey()
        {
            Assert.Equal("SD-12.pdf", ObjectExport.FileName(Record(), "pdf"));
            Assert.Equal("A_B_C.json", ObjectExport.FileName(new ObjectExportRecord { Key = "A/B\"C" }, "json"));
        }

        /// <summary>
        /// The CSV download address carries the semicolon delimiter for a culture writing the
        /// decimal separator as a comma, and no delimiter otherwise.
        /// </summary>
        [Fact]
        public void Csv_ParametersFollowTheCulture()
        {
            var german = CsvObjectExportFormat.ParametersFor(CultureInfo.GetCultureInfo("de-DE")).Single();

            Assert.Equal("delimiter", german.Key);
            Assert.Equal("semicolon", german.Value);
            Assert.Empty(CsvObjectExportFormat.ParametersFor(CultureInfo.GetCultureInfo("en-US")));
            Assert.Empty(((IObjectExportFormat)new JsonObjectExportFormat()).Parameters(null));
        }

        /// <summary>
        /// The JSON carries the machine values of the attributes, the description and per field
        /// name, type, stored value and display text; a password has no value.
        /// </summary>
        [Fact]
        public void Json_CarriesAttributesAndFields()
        {
            using var json = JsonDocument.Parse(JsonObjectExportFormat.ToJson(Record()));
            var root = json.RootElement;

            Assert.Equal("SD-12", root.GetProperty("key").GetString());
            Assert.Equal("2026-11-13T08:00:00Z", root.GetProperty("created").GetString());
            Assert.Equal("The printer on floor 2 is offline.", root.GetProperty("description").GetString());
            Assert.Equal("2026-11-13T09:00:00Z", root.GetProperty("exported").GetString());

            var fields = root.GetProperty("fields");

            Assert.Equal(3, fields.GetArrayLength());
            Assert.Equal("Priority", fields[0].GetProperty("name").GetString());
            Assert.Equal("Priority", fields[0].GetProperty("type").GetString());
            Assert.Equal("High", fields[0].GetProperty("value").GetString());
            Assert.Equal(JsonValueKind.Null, fields[1].GetProperty("value").ValueKind);
            Assert.Equal(JsonValueKind.Null, fields[2].GetProperty("text").ValueKind);
        }

        /// <summary>
        /// The JSON keeps non-ASCII text readable instead of escaping it.
        /// </summary>
        [Fact]
        public void Json_KeepsUmlauts()
        {
            var record = Record();
            var text = Encoding.UTF8.GetString(JsonObjectExportFormat.ToJson(new ObjectExportRecord
            {
                Key = record.Key,
                Attributes = record.Attributes,
                DescriptionText = "Drucker im Erdgeschoß",
                Exported = record.Exported
            }));

            Assert.Contains("Drucker im Erdgeschoß", text);
        }

        /// <summary>
        /// The JSON carries relations and comments as arrays and no history.
        /// </summary>
        [Fact]
        public void Json_CarriesRelationsAndComments()
        {
            using var json = JsonDocument.Parse(JsonObjectExportFormat.ToJson(Record()));
            var root = json.RootElement;

            Assert.Equal("SD-13", root.GetProperty("relations")[0].GetProperty("key").GetString());
            Assert.Equal("https://example.com/manual", root.GetProperty("relations")[1].GetProperty("uri").GetString());
            Assert.True(root.GetProperty("comments")[1].GetProperty("internal").GetBoolean());
            Assert.True(root.GetProperty("comments")[1].GetProperty("reply").GetBoolean());
            Assert.Equal("2026-11-13T08:30:00Z", root.GetProperty("comments")[0].GetProperty("created").GetString());
            Assert.False(root.TryGetProperty("history", out _));
        }

        /// <summary>
        /// The CSV starts with one header row (attribute labels, description, field names) and one
        /// value row (machine values, description text, field display texts).
        /// </summary>
        [Fact]
        public void Csv_WritesHeaderAndValueRow()
        {
            var lines = CsvObjectExportFormat.ToCsv(Record(), ';').ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            Assert.StartsWith("Key;Created;", lines[0]);
            Assert.EndsWith(";Priority;Secret;Notes", lines[0]);
            Assert.Equal("SD-12;2026-11-13T08:00:00Z;The printer on floor 2 is offline.;High;••••••••;", lines[1]);
        }

        /// <summary>
        /// Relations and comments follow the object rows as table blocks, each after an empty row.
        /// </summary>
        [Fact]
        public void Csv_WritesRelationAndCommentBlocks()
        {
            var lines = CsvObjectExportFormat.ToCsv(Record(), ';').ToString().Split("\r\n");

            Assert.Equal(string.Empty, lines[2]);
            Assert.Contains("blocks;SD-13;Toner order;Request;Active;", lines);
            Assert.Contains("documented in;;Vendor manual;;Active;https://example.com/manual", lines);
            Assert.Contains(lines, x => x.StartsWith("Ada;2026-11-13T08:30:00Z;") && x.EndsWith(";Restarted the spooler."));
            Assert.Contains(lines, x => x.StartsWith("Bob;2026-11-13T08:45:00Z;") && x.EndsWith(";Spooler crashes again."));
        }

        /// <summary>
        /// The PDF carries the title, the display texts of the attributes, the description and
        /// the field table; an empty value prints as the dash of the reading views.
        /// </summary>
        [Fact]
        public void Pdf_CarriesTitleAttributesDescriptionAndFields()
        {
            var document = PdfObjectExportFormat.ToPdf(Record());
            var text = document.PlainText;

            Assert.Equal("SD-12 Printer offline", document.Title);
            Assert.Contains("SD-12 Printer offline", text);
            Assert.Contains("13.11.2026 08:00", text);
            Assert.DoesNotContain("2026-11-13T08:00:00Z", text);
            Assert.Contains("The printer on floor 2 is offline.", text);
            Assert.Contains("High", text);
            Assert.Contains(global::KleeneStar.Core.WebControl.ObjectValueFormat.Empty, text);
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(document.ToArray(), 0, 4));
        }

        /// <summary>
        /// The PDF carries a section each for relations and comments.
        /// </summary>
        [Fact]
        public void Pdf_CarriesRelationsAndComments()
        {
            var text = PdfObjectExportFormat.ToPdf(Record()).PlainText;

            Assert.Contains("Toner order", text);
            Assert.Contains("Vendor manual", text);
            Assert.Contains("Restarted the spooler.", text);
            Assert.Contains("Spooler crashes again.", text);
        }

        /// <summary>
        /// A workbook description prints as one table per sheet: the sheet name as heading and
        /// the filled cells; a formula without a stored result prints the formula.
        /// </summary>
        [Fact]
        public void Pdf_PrintsAWorkbookAsTables()
        {
            var workbook = new SpreadsheetWorkbook();
            var sheet = workbook.AddSheet("Budget");

            sheet.SetValue("A1", "Item");
            sheet.SetValue("B1", "Q1");
            sheet.SetValue("A2", "Licenses");
            sheet.SetValue("B2", 8701);
            sheet.SetFormula("B3", "=SUM(B2:B2)");

            var record = new ObjectExportRecord
            {
                Key = "FIN-1",
                Summary = "Budget",
                Description = workbook.ToJson(),
                Exported = DateTime.UtcNow
            };

            var text = PdfObjectExportFormat.ToPdf(record).PlainText;

            Assert.Contains("Budget", text);
            Assert.Contains("Licenses", text);
            Assert.Contains("8701", text);
            Assert.Contains("=SUM(B2:B2)", text);
        }

        /// <summary>
        /// The formats write attachments named after the object key with their media type.
        /// </summary>
        [Fact]
        public void Formats_WriteAttachments()
        {
            foreach (var (format, type) in new (IObjectExportFormat, string)[]
            {
                (new PdfObjectExportFormat(), "application/pdf"),
                (new JsonObjectExportFormat(), "application/json"),
                (new CsvObjectExportFormat(), "text/csv")
            })
            {
                var response = format.Write(Record(), null);

                Assert.StartsWith(type, response.Header.ContentType);
                Assert.Equal($"attachment; filename=\"SD-12.{format.Extension}\"", response.Header.ContentDisposition);
            }
        }
    }
}
