using System;
using System.Globalization;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Converts the values of <c>Boolean</c> and <c>Date</c> fields between their storage form
    /// (<see cref="Model.Entities.Value.Data"/>), the form a dialog input loads and the form it
    /// submits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boolean: stored as <c>true</c> or <c>false</c>. <c>ControlFormItemInputCheck</c> renders a
    /// checkbox without <c>value</c> attribute, so a ticked box submits <c>on</c>;
    /// <see cref="IsTrue"/> reads <c>true</c>, <c>on</c>, <c>1</c>, <c>yes</c> and
    /// <c>checked</c> (ignoring case) as true, everything else as false. A dialog loads the value
    /// as a JSON boolean, because the form script sets <c>checked</c> from the truthiness of
    /// the loaded value and the string <c>false</c> is truthy.
    /// </para>
    /// <para>
    /// Date: stored in the round-trip format (<c>o</c>, invariant culture).
    /// <c>ControlFormItemInputDate</c> parses and submits the short date pattern of the request
    /// culture (<c>data-format</c>), so a dialog loads <see cref="ToDateInput"/> and the write
    /// path reads the submit with <see cref="TryParseDate"/>. The input carries no time; a
    /// submitted date without time equal to the date of the previous value keeps the previous
    /// value with its time, a submit with time (an API client) is stored as sent
    /// (<see cref="StoreDate"/>, <see cref="HasTime"/>).
    /// </para>
    /// </remarks>
    public static class FieldValueForm
    {
        /// <summary>
        /// Determines whether a stored or submitted value reads as true.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><see langword="true"/> for <c>true</c>, <c>on</c>, <c>1</c>, <c>yes</c>, <c>checked</c>.</returns>
        public static bool IsTrue(string value)
        {
            return value?.Trim().ToLowerInvariant() is "true" or "on" or "1" or "yes" or "checked";
        }

        /// <summary>
        /// Returns the storage form of a submitted boolean.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <returns><c>true</c> or <c>false</c>.</returns>
        public static string StoreBoolean(string value)
        {
            return IsTrue(value) ? "true" : "false";
        }

        /// <summary>
        /// Reads a stored or submitted date: the round-trip format and ISO 8601 first, then the
        /// short date pattern of the culture, then any date the culture parses.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="culture">The culture of the submit, or <see langword="null"/> (invariant).</param>
        /// <param name="date">The date.</param>
        /// <returns><see langword="true"/> when the value names a date.</returns>
        public static bool TryParseDate(string value, CultureInfo culture, out DateTime date)
        {
            date = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            var format = culture ?? CultureInfo.InvariantCulture;

            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) && LooksIso(text)
                || DateTime.TryParseExact(text, format.DateTimeFormat.ShortDatePattern, format, DateTimeStyles.None, out date)
                || DateTime.TryParse(text, format, DateTimeStyles.None, out date);
        }

        /// <summary>
        /// Returns the value a date input loads: the stored date in the short date pattern of the
        /// culture, or the stored text when it names no date.
        /// </summary>
        /// <param name="stored">The stored value.</param>
        /// <param name="culture">The culture of the request, or <see langword="null"/> (invariant).</param>
        /// <returns>The input value.</returns>
        public static string ToDateInput(string stored, CultureInfo culture)
        {
            var format = culture ?? CultureInfo.InvariantCulture;

            return TryParseDate(stored, format, out var date)
                ? date.ToString(format.DateTimeFormat.ShortDatePattern, format)
                : stored;
        }

        /// <summary>
        /// Returns the storage form of a submitted date: the previous value when the submit names
        /// the same day and no time (the date input), else the submitted date in the round-trip
        /// format; a value that names no date is stored as submitted.
        /// </summary>
        /// <param name="submitted">The submitted value.</param>
        /// <param name="previous">The previous stored value, or <see langword="null"/>.</param>
        /// <param name="culture">The culture of the submit, or <see langword="null"/> (invariant).</param>
        /// <returns>The value to store.</returns>
        public static string StoreDate(string submitted, string previous, CultureInfo culture)
        {
            if (!TryParseDate(submitted, culture, out var date))
            {
                return submitted;
            }

            if (!HasTime(submitted) && TryParseDate(previous, culture, out var before) && before.Date == date.Date)
            {
                return previous;
            }

            return date.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Determines whether a submitted date carries a time of day (a time separator), as an
        /// API client's ISO value does and the date input's short date pattern does not.
        /// </summary>
        /// <param name="text">The submitted text.</param>
        /// <returns><see langword="true"/> when the text names a time.</returns>
        public static bool HasTime(string text)
        {
            return text?.Contains(':') == true;
        }

        /// <summary>
        /// Determines whether a text is written in ISO 8601 (year first), so that an ambiguous
        /// culture date such as <c>11/12/2026</c> is not read with the invariant month order.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><see langword="true"/> for <c>yyyy-…</c>.</returns>
        private static bool LooksIso(string text)
        {
            return text.Length >= 10 && char.IsDigit(text[0]) && char.IsDigit(text[3]) && text[4] == '-';
        }
    }
}
