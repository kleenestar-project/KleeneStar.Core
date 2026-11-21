using System;
using System.Globalization;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Formats an item count for a badge: exact while it stays short, abbreviated by
    /// magnitude once it grows.
    /// </summary>
    /// <remarks>
    /// A badge is read at a glance beside a label, so an exact five-digit count costs more
    /// width than it carries meaning — past a few hundred the reader wants the order of
    /// magnitude, not the digit in the ones place. Counts below the threshold stay exact,
    /// because there the exact number is both short and the more useful answer.
    ///
    /// The decimal separator follows the reader's culture, so the same count renders as
    /// "1.3K" for an English reader and "1,3K" for a German one.
    /// </remarks>
    public static class CountBadgeFormat
    {
        /// <summary>
        /// The count from which on the abbreviated form is used.
        /// </summary>
        private const int Threshold = 500;

        /// <summary>
        /// Formats a count for display in a badge.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <param name="culture">
        /// The culture whose decimal separator applies. Falls back to the invariant culture.
        /// </param>
        /// <returns>
        /// The exact count below the threshold, the abbreviated one from it on, or null
        /// when there is nothing to count — a badge showing "0" reads as an error beside
        /// the populated entries, so an empty count is left off entirely.
        /// </returns>
        public static string Format(int count, CultureInfo culture = null)
        {
            culture ??= CultureInfo.InvariantCulture;

            if (count <= 0)
            {
                return null;
            }

            if (count < Threshold)
            {
                return count.ToString(culture);
            }

            // the thousands step is the floor rather than a lower bound, so a count of 500
            // reads as "0.5K": the abbreviation starts at the threshold, not at the first
            // full thousand
            var step = count switch
            {
                >= 1_000_000_000 => 3,
                >= 1_000_000 => 2,
                _ => 1
            };

            // one decimal is what separates 1.3K from 1.9K; it is dropped when it carries
            // nothing, so 2000 reads as "2K" rather than "2.0K"
            var value = Math.Round(count / Math.Pow(1_000d, step), 1, MidpointRounding.AwayFromZero);

            // rounding can push a count over its own magnitude - 999,999 divided by a
            // thousand rounds to 1000 - and "1000K" is exactly the number a badge is meant
            // to spare the reader, so the step moves up with it
            if (value >= 1_000d && step < 3)
            {
                value /= 1_000d;
                step++;
            }

            var suffix = step switch
            {
                3 => "B",
                2 => "M",
                _ => "K"
            };

            return value.ToString("0.#", culture) + suffix;
        }
    }
}
