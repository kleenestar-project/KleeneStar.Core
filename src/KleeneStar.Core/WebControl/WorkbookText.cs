using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WebExpress.WebUI.WebSpreadsheet;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Reads a description that is a workbook - the body of a document rendered as a
    /// spreadsheet - and tells it apart from an editor document.
    /// </summary>
    /// <remarks>
    /// The framework recognizes and reads workbook content. This adapter retains the
    /// application's preview limit, draft semantics and renderer conversion policy.
    /// </remarks>
    public static partial class WorkbookText
    {
        /// <summary>
        /// The longest plain text answered for a workbook. A sheet can hold far more than a
        /// table cell or a teaser shows, and every list would otherwise carry all of it.
        /// </summary>
        public const int MaxPlainTextLength = 1000;

        /// <summary>
        /// The longest workbook JSON accepted as the body of a document, in characters. The
        /// framework's own limit (<see cref="SpreadsheetWorkbook.MaxLength"/>) is meant for a
        /// control's transfer, whereas a body lives in a database row that every list, tile and
        /// teaser reads and parses again, so it is held far below that.
        /// </summary>
        public const int MaxLength = 8 * 1024 * 1024;

        /// <summary>
        /// The longest text the client keeps in one cell; a longer block of a converted text is
        /// continued in the rows below rather than cut.
        /// </summary>
        private const int MaxCellText = 32767;

        /// <summary>
        /// The width of the column a converted text is read in, in pixels.
        /// </summary>
        private const int ConvertedColumnWidth = 640;

        /// <summary>
        /// Determines whether a stored value is a workbook.
        /// </summary>
        /// <param name="value">The stored description.</param>
        /// <returns>True when the value is a JSON object with sheets and no editor document.</returns>
        public static bool IsWorkbook(string value)
        {
            return SpreadsheetWorkbook.IsWorkbook(value);
        }

        /// <summary>
        /// Reads a stored value as a workbook.
        /// </summary>
        /// <param name="value">The stored description.</param>
        /// <returns>The workbook, or <see langword="null"/> when the value is none.</returns>
        public static SpreadsheetWorkbook Read(string value)
        {
            return IsWorkbook(value) && SpreadsheetWorkbook.TryParse(value, out var workbook)
                ? workbook
                : null;
        }

        /// <summary>
        /// Answers the value the workbook dialog opens on: the stored workbook, or - when the
        /// description is no workbook, the text of a document whose class was switched to the
        /// spreadsheet renderer or the description a template preset - that text converted into
        /// a workbook, one block per row in the first column.
        /// </summary>
        /// <remarks>
        /// An empty sheet would let the first autosave and publish replace the text with nothing
        /// the author ever saw. Converted, the words are what the author edits and publishes;
        /// only the formatting is left behind, and the original stays in the history.
        /// </remarks>
        /// <param name="value">The stored description.</param>
        /// <returns>The workbook JSON, or <see langword="null"/> for an empty description.</returns>
        public static string ForEditor(string value)
        {
            if (IsWorkbook(value))
            {
                return value;
            }

            return ProseText.IsEmpty(value) ? null : FromProse(value);
        }

        /// <summary>
        /// Converts a description that is no workbook into one: every block of its text - a
        /// paragraph, a heading, a list item, a line - becomes a row of the first column.
        /// </summary>
        /// <param name="value">The stored description: an editor document or text.</param>
        /// <returns>The workbook JSON.</returns>
        private static string FromProse(string value)
        {
            // a table cell ends with a blank rather than a break, so a row of a table stays one line
            var markup = BlockEnds().Replace(ProseText.ToHtml(value) ?? string.Empty, end => end.Groups["cell"].Success ? " " : "\n");
            var lines = WebUtility.HtmlDecode(Tags().Replace(markup, string.Empty))
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(line => line.Chunk(MaxCellText).Select(part => new string(part)))
                .Take(SpreadsheetAddress.MaxRows)
                .ToList();

            var workbook = SpreadsheetWorkbook.Parse(null);
            var sheet = workbook.Sheets[0];

            // set as a value, so a line that starts with "=" stays the text it was
            for (var row = 0; row < lines.Count; row++)
            {
                sheet.SetCell(row, 0, new SpreadsheetCell { Value = lines[row] });
            }

            sheet.ColumnWidths[0] = ConvertedColumnWidth;

            return workbook.ToJson();
        }

        /// <summary>
        /// Validates a submitted workbook and answers it in the form it is stored in.
        /// </summary>
        /// <remarks>
        /// The workbook is written back <i>with</i> the formula results the client posted, so a
        /// list reads the figures of a formula cell without a formula engine. A blank value is
        /// accepted as the empty workbook. A null value keeps the draft manager's unchanged semantics.
        /// A value longer than <see cref="MaxLength"/> is refused before it is parsed.
        /// </remarks>
        /// <param name="value">The submitted workbook JSON.</param>
        /// <param name="normalized">Receives the workbook to store, or null for an unchanged body or failed validation.</param>
        /// <returns>True when the value is a workbook within the limit or blank.</returns>
        public static bool TryNormalize(string value, out string normalized)
        {
            if (value is null)
            {
                normalized = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                // null means unchanged to the draft manager, so clearing needs an explicit workbook
                normalized = SpreadsheetWorkbook.Parse(null).ToJson();
                return true;
            }

            if (value.Length > MaxLength)
            {
                normalized = null;
                return false;
            }

            var workbook = Read(value);

            normalized = workbook?.ToJsonObject(includeResults: true).ToJsonString();

            return workbook is not null;
        }

        /// <summary>
        /// Returns the words of a workbook: the shown value of every filled cell, sheet after
        /// sheet in row-major order, separated by a single space and cut at
        /// <see cref="MaxPlainTextLength"/>.
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <returns>The text, empty for an empty workbook.</returns>
        public static string ToPlainText(SpreadsheetWorkbook workbook)
        {
            if (workbook is null)
            {
                return null;
            }

            var text = workbook.ToPlainText();
            return text.Length >= MaxPlainTextLength
                ? text[..MaxPlainTextLength].TrimEnd() + "…"
                : text;
        }

        [GeneratedRegex(@"</(?:(?<cell>t[dh])|p|h[1-6]|li|div|blockquote|pre|tr|figcaption)>|<br\s*/?>", RegexOptions.IgnoreCase)]
        private static partial Regex BlockEnds();

        [GeneratedRegex("<[^>]*>")]
        private static partial Regex Tags();
    }
}
