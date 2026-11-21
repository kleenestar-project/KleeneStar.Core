using KleeneStar.Core.WebControl;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebEditor;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPdf;
using WebExpress.WebUI.WebPdf.Element;
using WebExpress.WebUI.WebSpreadsheet;

namespace KleeneStar.Core.WebReport.Formats
{
    /// <summary>
    /// The PDF export of an object (<see cref="PdfDocument"/> of WebExpress).
    /// </summary>
    /// <remarks>
    /// Heading <c>{key} {summary}</c>, attribute table (display texts), description
    /// (<see cref="EditorContent.ConvertToPdf"/>; a workbook as one table per visible sheet, at
    /// most <see cref="MaxSheetRows"/> rows and <see cref="MaxSheetColumns"/> columns, a formula
    /// without stored result as the formula), field table, a section for relations (table) and
    /// one for comments (author, date, internal note and reply markers, content through
    /// <see cref="EditorContent.ConvertToPdf"/>, replies indented), footer
    /// <c>object.export.pdf.footer</c>.
    /// </remarks>
    public sealed class PdfObjectExportFormat : IObjectExportFormat
    {
        /// <summary>
        /// The most rows of a sheet the PDF prints.
        /// </summary>
        public const int MaxSheetRows = 500;

        /// <summary>
        /// The most columns of a sheet the PDF prints.
        /// </summary>
        public const int MaxSheetColumns = 26;

        /// <summary>
        /// The indent of a reply, in points.
        /// </summary>
        private const float ReplyIndent = 18f;

        /// <inheritdoc/>
        public string Key => "pdf";

        /// <inheritdoc/>
        public string Extension => "pdf";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:object.export.format.pdf.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:object.export.format.pdf.description";

        /// <inheritdoc/>
        public string Content => "kleenestar.core:object.export.format.pdf.content";

        /// <inheritdoc/>
        public string Badge => "PDF";

        /// <inheritdoc/>
        public TypeColorTile Color => TypeColorTile.Danger;

        /// <inheritdoc/>
        public IIcon Icon => new IconFilePdf();

        /// <inheritdoc/>
        public int Order => 10;

        /// <inheritdoc/>
        public IResponse Write(ObjectExportRecord record, IRequest request)
        {
            return ObjectExport.Attachment(ToPdf(record).ToArray(), "application/pdf", ObjectExport.FileName(record, Extension));
        }

        /// <summary>
        /// Writes the record as PDF in the record culture.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <returns>The document.</returns>
        public static PdfDocument ToPdf(ObjectExportRecord record)
        {
            string T(string key) => I18N.Translate(record.Culture, "kleenestar.core:object.export." + key);

            var document = new PdfDocument
            {
                Title = $"{record.Key} {record.Summary}".Trim(),
                Subject = record.Summary,
                Language = record.Culture.TwoLetterISOLanguageName,
                CreationDate = new DateTimeOffset(DateTime.SpecifyKind(record.Exported, DateTimeKind.Utc)),
                Footer = T("pdf.footer")
            };

            document.Add(new PdfBlockElementHeading(1, document.Title));
            document.Add(Table(record.Attributes.Select(x => (x.Label, x.Text))));

            document.Add(new PdfBlockElementHeading(2, T("attribute.description")));

            if (ProseText.IsEmpty(record.Description))
            {
                document.Add(new PdfBlockElementParagraph(ObjectValueFormat.Empty));
            }
            else if (WorkbookText.Read(record.Description) is SpreadsheetWorkbook workbook)
            {
                document.Add(Sheets(workbook, record.Culture));
            }
            else
            {
                document.Add(EditorContent.ConvertToPdf(record.Description).Elements);
            }

            if (record.Fields.Count > 0)
            {
                document.Add(new PdfBlockElementHeading(2, T("pdf.fields")));
                document.Add(Table(record.Fields.Select(x => (x.Name, x.Text))));
            }

            document.Add(Relations(record, T));
            document.Add(Comments(record, T));

            return document;
        }

        /// <summary>
        /// Returns the relations section.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="translate">Translates a key below <c>kleenestar.core:object.export.</c>.</param>
        /// <returns>The blocks.</returns>
        private static IEnumerable<PdfBlockElement> Relations(ObjectExportRecord record, Func<string, string> translate)
        {
            yield return new PdfBlockElementHeading(2, translate("section.relations"));

            if (record.Relations.Count == 0)
            {
                yield return new PdfBlockElementParagraph(translate("relations.none"));

                yield break;
            }

            yield return Grid
            (
                [translate("relation.type"), translate("relation.key"), translate("relation.title"), translate("relation.status")],
                [1.2f, 1f, 2.2f, 0.8f],
                record.Relations.Select(x => new[] { x.Type, x.Key ?? x.Uri, x.Title, x.Status })
            );
        }

        /// <summary>
        /// Returns the comments section.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="translate">Translates a key below <c>kleenestar.core:object.export.</c>.</param>
        /// <returns>The blocks.</returns>
        private static IEnumerable<PdfBlockElement> Comments(ObjectExportRecord record, Func<string, string> translate)
        {
            var bold = new PdfTextStyle { Bold = true };

            yield return new PdfBlockElementHeading(2, translate("section.comments"));

            if (record.Comments.Count == 0)
            {
                yield return new PdfBlockElementParagraph(translate("comments.none"));

                yield break;
            }

            foreach (var comment in record.Comments)
            {
                var indent = comment.Reply ? ReplyIndent : 0f;
                var line = string.Join(" · ", new[]
                {
                    comment.Author,
                    DateTime.SpecifyKind(comment.Created, DateTimeKind.Utc).ToString("g", record.Culture),
                    comment.Internal ? translate("comment.internal") : null,
                    comment.Reply ? translate("comment.reply") : null
                }.Where(x => !string.IsNullOrWhiteSpace(x)));

                yield return new PdfBlockElementParagraph(line, bold) { Indent = indent };

                if (!EditorState.IsState(comment.Content))
                {
                    yield return new PdfBlockElementParagraph(comment.Text ?? string.Empty) { Indent = indent };

                    continue;
                }

                foreach (var block in EditorContent.ConvertToPdf(comment.Content).Elements)
                {
                    if (block is PdfBlockElementParagraph paragraph)
                    {
                        paragraph.Indent = indent;
                    }

                    yield return block;
                }
            }
        }

        /// <summary>
        /// Builds one table per visible sheet of a workbook: a heading with the sheet name, then
        /// the rows and columns between the first and the last filled cell, without hidden rows
        /// and columns, capped at <see cref="MaxSheetRows"/> rows and <see cref="MaxSheetColumns"/>
        /// columns.
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="culture">The culture of the values.</param>
        /// <returns>The blocks.</returns>
        private static IEnumerable<PdfBlockElement> Sheets(SpreadsheetWorkbook workbook, CultureInfo culture)
        {
            foreach (var sheet in workbook.Sheets.Where(x => !x.Hidden))
            {
                var cells = sheet.EnumerateCells().Where(x => !x.Cell.IsEmpty).ToList();

                if (cells.Count == 0)
                {
                    continue;
                }

                var firstRow = cells.Min(x => x.Row);
                var firstColumn = cells.Min(x => x.Column);
                var rows = Enumerable.Range(firstRow, cells.Max(x => x.Row) - firstRow + 1)
                    .Where(row => !sheet.HiddenRows.Contains(row))
                    .Take(MaxSheetRows)
                    .ToList();
                var columns = Enumerable.Range(firstColumn, cells.Max(x => x.Column) - firstColumn + 1)
                    .Where(column => !sheet.HiddenColumns.Contains(column))
                    .Take(MaxSheetColumns)
                    .ToList();

                yield return new PdfBlockElementHeading(3, sheet.Name);

                var table = new PdfBlockElementTable { Striped = true }
                    .SetColumnWidths(columns.Select(_ => 1f));

                table.Add(rows.Select(row => new PdfBlockElementTableRow()
                    .Add(columns.Select(column => new PdfBlockElementTableCell(CellText(sheet.GetCell(row, column), culture))))));

                yield return table;
            }
        }

        /// <summary>
        /// Returns the text of a sheet cell: the effective value in the culture, the formula when
        /// no result is stored, empty for an empty cell.
        /// </summary>
        /// <param name="cell">The cell, or <see langword="null"/>.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The text.</returns>
        private static string CellText(SpreadsheetCell cell, CultureInfo culture)
        {
            if (cell is null || cell.IsEmpty)
            {
                return string.Empty;
            }

            var value = cell.EffectiveValue;

            if (value is null && !string.IsNullOrEmpty(cell.Formula))
            {
                return cell.Formula.StartsWith('=') ? cell.Formula : "=" + cell.Formula;
            }

            return Convert.ToString(value, culture) ?? string.Empty;
        }

        /// <summary>
        /// Builds a two-column table of labels and texts; an empty text prints as
        /// <see cref="ObjectValueFormat.Empty"/>.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <returns>The table.</returns>
        private static PdfBlockElementTable Table(IEnumerable<(string Label, string Text)> rows)
        {
            var table = new PdfBlockElementTable { Striped = true }
                .SetColumnWidths([1f, 2f]);

            table.Add(rows.Select(row => new PdfBlockElementTableRow()
                .Add
                (
                    new PdfBlockElementTableCell().Add(new PdfBlockElementParagraph(row.Label ?? string.Empty, new PdfTextStyle { Bold = true })),
                    new PdfBlockElementTableCell(string.IsNullOrWhiteSpace(row.Text) ? ObjectValueFormat.Empty : row.Text)
                )));

            return table;
        }

        /// <summary>
        /// Builds a table with a header row.
        /// </summary>
        /// <param name="headers">The column headers.</param>
        /// <param name="widths">The relative column widths.</param>
        /// <param name="rows">The rows.</param>
        /// <returns>The table.</returns>
        private static PdfBlockElementTable Grid(string[] headers, float[] widths, IEnumerable<string[]> rows)
        {
            var table = new PdfBlockElementTable { Striped = true }
                .SetColumnWidths(widths);

            table.Add(new PdfBlockElementTableRow { Header = true }
                .Add(headers.Select(x => new PdfBlockElementTableCell().Add(new PdfBlockElementParagraph(x ?? string.Empty, new PdfTextStyle { Bold = true })))));

            table.Add(rows.Select(row => new PdfBlockElementTableRow()
                .Add(row.Select(x => new PdfBlockElementTableCell(x ?? string.Empty)))));

            return table;
        }
    }
}
