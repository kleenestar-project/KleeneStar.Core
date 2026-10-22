using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WebExpress.WebUI.WebSpreadsheet;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Reads a description that is a workbook - the body of a document rendered as a
    /// spreadsheet - and tells it apart from an editor document.
    /// </summary>
    /// <remarks>
    /// A workbook and a prose document are both stored as JSON in
    /// <see cref="Model.Entities.Object.Description"/>, and the framework's own tests cannot tell
    /// them apart: <see cref="SpreadsheetWorkbook.IsWorkbook"/> and
    /// <c>EditorState.IsState</c> both answer "starts with a brace", and
    /// <see cref="SpreadsheetWorkbook.Parse"/> reads an editor document as a workbook with one
    /// empty sheet. A workbook is therefore recognised here by what only it carries - a
    /// <c>sheets</c> array and no <c>doc</c> - before anything reads it as one.
    /// <see cref="ProseText"/> asks this class first, so every list, tile and teaser shows the
    /// words of a sheet instead of its serialization.
    /// </remarks>
    public static class WorkbookText
    {
        /// <summary>
        /// The longest plain text answered for a workbook. A sheet can hold far more than a
        /// table cell or a teaser shows, and every list would otherwise carry all of it.
        /// </summary>
        public const int MaxPlainTextLength = 1000;

        /// <summary>
        /// Determines whether a stored value is a workbook.
        /// </summary>
        /// <param name="value">The stored description.</param>
        /// <returns>True when the value is a JSON object with sheets and no editor document.</returns>
        public static bool IsWorkbook(string value)
        {
            // a cheap look before the parse, because every prose description passes through here
            if (!SpreadsheetWorkbook.IsWorkbook(value) || !value.Contains("\"sheets\"", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return JsonNode.Parse(value, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) is JsonObject root
                    && root["sheets"] is JsonArray
                    && root["doc"] is null;
            }
            catch (JsonException)
            {
                return false;
            }
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
        /// Answers the value the workbook dialog opens on: the stored workbook, or nothing when
        /// the description is no workbook - the text of a document whose class was switched to
        /// the spreadsheet renderer, or the description a template preset. The sheet then opens
        /// empty rather than on a value it cannot read; the text stays in the history.
        /// </summary>
        /// <param name="value">The stored description.</param>
        /// <returns>The workbook JSON, or <see langword="null"/>.</returns>
        public static string ForEditor(string value)
        {
            return IsWorkbook(value) ? value : null;
        }

        /// <summary>
        /// Validates a submitted workbook and answers it in the form it is stored in.
        /// </summary>
        /// <remarks>
        /// The workbook is written back <i>with</i> the formula results the client posted, so a
        /// list reads the figures of a formula cell without a formula engine. A blank value is
        /// accepted as the empty workbook.
        /// </remarks>
        /// <param name="value">The submitted workbook JSON.</param>
        /// <param name="normalized">Receives the workbook to store, or the blank value.</param>
        /// <returns>True when the value is a workbook or blank.</returns>
        public static bool TryNormalize(string value, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                normalized = null;
                return true;
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

            var builder = new StringBuilder();

            foreach (var text in workbook.Sheets
                .SelectMany(sheet => sheet.EnumerateCells())
                .Select(cell => Format(cell.Cell.EffectiveValue))
                .Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(text.Trim());

                if (builder.Length >= MaxPlainTextLength)
                {
                    return builder.ToString(0, MaxPlainTextLength).TrimEnd() + "…";
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats a cell value for reading.
        /// </summary>
        /// <param name="value">The effective value of a cell.</param>
        /// <returns>The text.</returns>
        private static string Format(object value)
        {
            return value switch
            {
                null => null,
                string text => text,

                // a typed cell (a link) carries its caption beside its address
                JsonObject node => node["text"] is JsonValue caption && caption.TryGetValue<string>(out var text) ? text : null,
                JsonNode node => node.ToJsonString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }
    }
}
