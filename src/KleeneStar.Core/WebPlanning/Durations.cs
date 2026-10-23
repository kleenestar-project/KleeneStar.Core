using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KleeneStar.Core.WebPlanning
{
    /// <summary>
    /// Reads and writes durations the way people type them: <c>1h 30m</c>, <c>90m</c>,
    /// <c>1.5h</c>, <c>2d</c>, <c>1w</c>.
    /// </summary>
    /// <remarks>
    /// A day is a working day of eight hours and a week five of them - an estimate counts the
    /// time of work, not the time of the calendar. A bare number is minutes.
    /// </remarks>
    public static partial class Durations
    {
        /// <summary>The minutes of a working day.</summary>
        public const int Day = 8 * 60;

        /// <summary>The minutes of a working week.</summary>
        public const int Week = 5 * Day;

        [GeneratedRegex(@"(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>w|d|h|m)?", RegexOptions.IgnoreCase)]
        private static partial Regex PartRegex();

        /// <summary>
        /// Reads a duration.
        /// </summary>
        /// <param name="text">The duration as typed.</param>
        /// <returns>The minutes, or <see langword="null"/> when the text is no duration.</returns>
        public static int? Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var rest = text.Trim();
            double minutes = 0;
            var matched = false;

            foreach (Match part in PartRegex().Matches(rest))
            {
                var value = double.Parse(part.Groups["value"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);

                minutes += part.Groups["unit"].Value.ToLowerInvariant() switch
                {
                    "w" => value * Week,
                    "d" => value * Day,
                    "h" => value * 60,
                    _ => value
                };

                rest = rest.Replace(part.Value, string.Empty);
                matched = true;
            }

            // anything left over that is not blank makes the text something else than a duration
            return matched && rest.Trim().Length == 0 ? (int)Math.Round(minutes) : null;
        }

        /// <summary>
        /// Writes a duration.
        /// </summary>
        /// <param name="minutes">The minutes.</param>
        /// <returns><c>1d 2h 30m</c>, or <c>0m</c>.</returns>
        public static string Format(int? minutes)
        {
            if (minutes is not int value)
            {
                return "-";
            }

            if (value == 0)
            {
                return "0m";
            }

            var text = new StringBuilder(value < 0 ? "-" : string.Empty);
            var rest = Math.Abs(value);

            foreach (var (size, unit) in new[] { (Day, "d"), (60, "h"), (1, "m") })
            {
                if (rest >= size)
                {
                    text.Append(rest / size).Append(unit).Append(' ');
                    rest %= size;
                }
            }

            return text.ToString().TrimEnd();
        }
    }
}
