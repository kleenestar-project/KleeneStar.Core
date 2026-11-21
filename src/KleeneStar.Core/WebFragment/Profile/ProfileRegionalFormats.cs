using System;
using System.Collections.Generic;
using System.Globalization;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The time zones and date patterns the account page offers, plus the labels that show what
    /// choosing one of them actually does.
    /// </summary>
    /// <remarks>
    /// The time-zone list is curated rather than the full IANA database for the same reason the
    /// dialling prefixes are: the setting is picked once and a list of six hundred entries is
    /// harder to use than a short one that covers the installations this product serves.
    /// </remarks>
    internal static class ProfileRegionalFormats
    {
        /// <summary>
        /// Gets the offered time zones as (IANA id, label) pairs.
        /// </summary>
        public static IReadOnlyList<(string Id, string Label)> TimeZones { get; } =
        [
            ("Europe/Berlin", "Europe/Berlin"),
            ("Europe/Vienna", "Europe/Vienna"),
            ("Europe/Zurich", "Europe/Zurich"),
            ("Europe/London", "Europe/London"),
            ("Europe/Lisbon", "Europe/Lisbon"),
            ("Europe/Madrid", "Europe/Madrid"),
            ("Europe/Paris", "Europe/Paris"),
            ("Europe/Rome", "Europe/Rome"),
            ("Europe/Warsaw", "Europe/Warsaw"),
            ("Europe/Helsinki", "Europe/Helsinki"),
            ("Europe/Kyiv", "Europe/Kyiv"),
            ("America/New_York", "America/New_York"),
            ("America/Chicago", "America/Chicago"),
            ("America/Denver", "America/Denver"),
            ("America/Los_Angeles", "America/Los_Angeles"),
            ("America/Sao_Paulo", "America/Sao_Paulo"),
            ("Asia/Jerusalem", "Asia/Jerusalem"),
            ("Asia/Dubai", "Asia/Dubai"),
            ("Asia/Kolkata", "Asia/Kolkata"),
            ("Asia/Singapore", "Asia/Singapore"),
            ("Asia/Tokyo", "Asia/Tokyo"),
            ("Australia/Sydney", "Australia/Sydney"),
            ("UTC", "UTC")
        ];

        /// <summary>
        /// Gets the offered date patterns, written the way .NET formats with them.
        /// </summary>
        public static IReadOnlyList<string> DatePatterns { get; } =
        [
            "dd.MM.yyyy",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "d. MMMM yyyy"
        ];

        /// <summary>
        /// Describes the automatic time-zone entry by naming the zone the server currently
        /// runs in and its offset, so the entry says what it will do rather than only that it
        /// will do something.
        /// </summary>
        /// <returns>The label of the automatic entry.</returns>
        public static string DescribeAutomaticTimeZone()
        {
            var local = TimeZoneInfo.Local;
            var offset = local.GetUtcOffset(DateTime.Now);
            var sign = offset < TimeSpan.Zero ? "-" : "+";

            return $"{local.Id} · GMT{sign}{offset:hh\\:mm}";
        }

        /// <summary>
        /// Describes a date pattern by rendering a sample date with it, followed by the pattern
        /// itself, so the entry can be recognized without knowing the format letters.
        /// </summary>
        /// <param name="pattern">The .NET date pattern to describe.</param>
        /// <returns>The label of the pattern entry.</returns>
        public static string DescribeDatePattern(string pattern)
        {
            var sample = new DateTime(2026, 5, 21);

            try
            {
                return $"{sample.ToString(pattern, CultureInfo.CurrentCulture)} ({pattern})";
            }
            catch (FormatException)
            {
                return pattern;
            }
        }
    }
}
