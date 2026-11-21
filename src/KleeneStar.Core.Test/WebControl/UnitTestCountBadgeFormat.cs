using KleeneStar.Core.WebControl;
using System.Globalization;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="CountBadgeFormat"/> — the item count a sidebar
    /// badge shows: exact while it stays short, abbreviated by magnitude once it grows.
    /// </summary>
    public class UnitTestCountBadgeFormat
    {
        /// <summary>
        /// Verifies that a count below the threshold keeps every digit, because there the
        /// exact number is both short and the more useful answer.
        /// </summary>
        /// <param name="count">The count under test.</param>
        /// <param name="expected">The expected badge text.</param>
        [Theory]
        [InlineData(1, "1")]
        [InlineData(20, "20")]
        [InlineData(120, "120")]
        [InlineData(499, "499")]
        public void BelowTheThreshold_IsExact(int count, string expected)
        {
            Assert.Equal(expected, CountBadgeFormat.Format(count, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Verifies the abbreviation from the threshold on. The thousands step is the floor
        /// rather than a lower bound, so the hundreds are abbreviated too.
        /// </summary>
        /// <param name="count">The count under test.</param>
        /// <param name="expected">The expected badge text.</param>
        [Theory]
        [InlineData(500, "0.5K")]
        [InlineData(512, "0.5K")]
        [InlineData(847, "0.8K")]
        [InlineData(999, "1K")]
        [InlineData(1000, "1K")]
        [InlineData(1342, "1.3K")]
        [InlineData(2000, "2K")]
        [InlineData(12800, "12.8K")]
        [InlineData(1000000, "1M")]
        [InlineData(4700000, "4.7M")]
        [InlineData(1000000000, "1B")]
        public void FromTheThreshold_IsAbbreviated(int count, string expected)
        {
            Assert.Equal(expected, CountBadgeFormat.Format(count, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Verifies that a count rounding over its own magnitude moves up a step. A badge
        /// reading "1000K" would show exactly the number it exists to spare the reader.
        /// </summary>
        /// <param name="count">The count under test.</param>
        /// <param name="expected">The expected badge text.</param>
        [Theory]
        [InlineData(999, "1K")]
        [InlineData(999999, "1M")]
        [InlineData(999999999, "1B")]
        public void ACountRoundingOverItsMagnitude_MovesUp(int count, string expected)
        {
            Assert.Equal(expected, CountBadgeFormat.Format(count, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Verifies that the decimal separator follows the reader's culture, so the same
        /// count reads naturally in either language.
        /// </summary>
        [Fact]
        public void TheSeparator_FollowsTheCulture()
        {
            Assert.Equal("1.3K", CountBadgeFormat.Format(1342, new CultureInfo("en-US")));
            Assert.Equal("1,3K", CountBadgeFormat.Format(1342, new CultureInfo("de-DE")));
            Assert.Equal("4,7M", CountBadgeFormat.Format(4700000, new CultureInfo("de-DE")));
        }

        /// <summary>
        /// Verifies that an empty count yields no badge at all. A "0" beside the populated
        /// entries reads as an error rather than as "nothing filed yet".
        /// </summary>
        /// <param name="count">The count under test.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void AnEmptyCount_ShowsNoBadge(int count)
        {
            Assert.Null(CountBadgeFormat.Format(count, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Verifies that a missing culture does not throw, so a caller without a request
        /// culture still gets a badge.
        /// </summary>
        [Fact]
        public void WithoutACulture_TheInvariantOneApplies()
        {
            Assert.Equal("1.3K", CountBadgeFormat.Format(1342));
        }
    }
}
