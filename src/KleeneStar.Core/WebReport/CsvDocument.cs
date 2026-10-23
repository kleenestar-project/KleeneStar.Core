using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebReport
{
    /// <summary>
    /// Writes a table as CSV (RFC 4180) for a spreadsheet to open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file starts with a UTF-8 byte order mark, without which Excel reads umlauts wrong.
    /// The separator is a comma, or a semicolon (<c>?delimiter=semicolon</c>) for the
    /// spreadsheets of locales whose decimal mark is the comma.
    /// </para>
    /// <para>
    /// A cell starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage return
    /// is prefixed with an apostrophe: a spreadsheet would run it as a formula, and an object's
    /// summary is written by whoever filed it (CSV injection).
    /// </para>
    /// </remarks>
    public sealed class CsvDocument
    {
        private readonly StringBuilder _text = new();
        private readonly char _delimiter;

        /// <summary>
        /// Gets the culture numbers and dates are written in: invariant, so a number reads the
        /// same whatever the reader's language.
        /// </summary>
        public static CultureInfo Culture => CultureInfo.InvariantCulture;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="delimiter">The separator.</param>
        public CsvDocument(char delimiter = ',')
        {
            _delimiter = delimiter;
        }

        /// <summary>
        /// Reads the separator a request asks for.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A semicolon for <c>?delimiter=semicolon</c> (or <c>;</c>), else a comma.</returns>
        public static char DelimiterOf(IRequest request)
        {
            var value = request?.GetParameter("delimiter")?.Value?.Trim();

            return string.Equals(value, "semicolon", StringComparison.OrdinalIgnoreCase) || value == ";" ? ';' : ',';
        }

        /// <summary>
        /// Appends a row.
        /// </summary>
        /// <param name="cells">The cells.</param>
        /// <returns>The document, for chaining.</returns>
        public CsvDocument Row(IEnumerable<string> cells)
        {
            _text.Append(string.Join(_delimiter, (cells ?? []).Select(Escape)));
            _text.Append("\r\n");

            return this;
        }

        /// <summary>
        /// Appends a row.
        /// </summary>
        /// <param name="cells">The cells.</param>
        /// <returns>The document, for chaining.</returns>
        public CsvDocument Row(params string[] cells)
        {
            return Row((IEnumerable<string>)cells);
        }

        /// <summary>
        /// Formats a date and time for a cell.
        /// </summary>
        /// <param name="value">The value, in UTC.</param>
        /// <returns>ISO 8601, or empty.</returns>
        public static string Date(DateTime? value)
        {
            return value is DateTime date ? DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Culture) : string.Empty;
        }

        /// <summary>
        /// Formats a number for a cell.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The number, or empty.</returns>
        public static string Number(double? value)
        {
            return value is double number ? number.ToString("0.##", Culture) : string.Empty;
        }

        /// <summary>
        /// Returns the document as a file download.
        /// </summary>
        /// <param name="fileName">The name the browser saves it under.</param>
        /// <returns>The response.</returns>
        public IResponse ToResponse(string fileName)
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_text.ToString())).ToArray();
            var response = new ResponseOK { Content = bytes };

            response.Header.ContentType = "text/csv; charset=utf-8";
            response.Header.ContentLength = bytes.Length;
            response.Header.ContentDisposition = $"attachment; filename=\"{Sanitize(fileName)}\"";

            return response;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _text.ToString();
        }

        /// <summary>
        /// Escapes a cell.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The cell as written.</returns>
        private string Escape(string value)
        {
            var text = value ?? string.Empty;

            if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            {
                text = "'" + text;
            }

            return text.IndexOfAny([_delimiter, '"', '\r', '\n']) >= 0
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
        }

        /// <summary>
        /// Keeps a file name to the characters every browser and file system accepts.
        /// </summary>
        /// <param name="fileName">The name.</param>
        /// <returns>The safe name.</returns>
        private static string Sanitize(string fileName)
        {
            var safe = new string([.. (fileName ?? "export.csv").Select(x => char.IsLetterOrDigit(x) || x is '-' or '_' or '.' ? x : '-')]);

            return safe.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? safe : safe + ".csv";
        }
    }
}
