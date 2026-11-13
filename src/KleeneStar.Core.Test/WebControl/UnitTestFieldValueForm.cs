using KleeneStar.Core.WebControl;
using System.Globalization;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Tests <see cref="FieldValueForm"/>: booleans submitted by a checkbox without value, dates
    /// loaded into and submitted by the date input in the short date pattern of the culture.
    /// </summary>
    public class UnitTestFieldValueForm
    {
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        /// <summary>
        /// The submit of a ticked checkbox without value (<c>on</c>) and the stored forms read as
        /// true; everything else reads as false.
        /// </summary>
        [Theory]
        [InlineData("on", true)]
        [InlineData("true", true)]
        [InlineData("True", true)]
        [InlineData("1", true)]
        [InlineData("yes", true)]
        [InlineData("checked", true)]
        [InlineData("false", false)]
        [InlineData("off", false)]
        [InlineData("0", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTrue_ReadsTheCheckboxIdiom(string value, bool expected)
        {
            Assert.Equal(expected, FieldValueForm.IsTrue(value));
        }

        /// <summary>
        /// A submitted boolean is stored as <c>true</c> or <c>false</c>.
        /// </summary>
        [Fact]
        public void StoreBoolean_WritesTrueOrFalse()
        {
            Assert.Equal("true", FieldValueForm.StoreBoolean("on"));
            Assert.Equal("false", FieldValueForm.StoreBoolean("false"));
        }

        /// <summary>
        /// The stored round-trip value loads in the short date pattern of the culture, which the
        /// date input parses.
        /// </summary>
        [Fact]
        public void ToDateInput_WritesTheShortDatePattern()
        {
            Assert.Equal("13.11.2026", FieldValueForm.ToDateInput("2026-11-13T18:00:08.5404471", German));
            Assert.Equal("11/13/2026", FieldValueForm.ToDateInput("2026-11-13T18:00:08.5404471", English));
            Assert.Equal("13.11.2026", FieldValueForm.ToDateInput("13.11.2026", German));
            Assert.Equal("not a date", FieldValueForm.ToDateInput("not a date", German));
        }

        /// <summary>
        /// A submit in the culture pattern is read in that culture; ISO is read year first.
        /// </summary>
        [Fact]
        public void TryParseDate_ReadsCultureAndIso()
        {
            Assert.True(FieldValueForm.TryParseDate("12.11.2026", German, out var german));
            Assert.Equal(new DateTime(2026, 11, 12), german.Date);

            Assert.True(FieldValueForm.TryParseDate("11/12/2026", English, out var english));
            Assert.Equal(new DateTime(2026, 11, 12), english.Date);

            Assert.True(FieldValueForm.TryParseDate("2026-11-12T08:30:00.0000000", German, out var iso));
            Assert.Equal(new DateTime(2026, 11, 12, 8, 30, 0), iso);

            Assert.False(FieldValueForm.TryParseDate("", German, out _));
            Assert.False(FieldValueForm.TryParseDate("tomorrow", German, out _));
        }

        /// <summary>
        /// A submitted date of the same day keeps the previous value with its time; another day
        /// is stored in the round-trip format; a value naming no date is stored as submitted.
        /// </summary>
        [Fact]
        public void StoreDate_KeepsThePreviousTimeOnTheSameDay()
        {
            const string previous = "2026-11-13T18:00:08.5404471";

            Assert.Equal(previous, FieldValueForm.StoreDate("13.11.2026", previous, German));
            Assert.Equal("2026-11-20T00:00:00.0000000", FieldValueForm.StoreDate("20.11.2026", previous, German));
            Assert.Equal("2026-11-20T00:00:00.0000000", FieldValueForm.StoreDate("20.11.2026", null, German));
            Assert.Equal("someday", FieldValueForm.StoreDate("someday", previous, German));
        }

        /// <summary>
        /// A submit with an explicit time is stored as sent, also on the same day as the previous
        /// value; only a submit without time keeps the previous time.
        /// </summary>
        [Fact]
        public void StoreDate_StoresAnExplicitTime()
        {
            const string previous = "2026-11-13T08:00:00Z";

            Assert.Equal("2026-11-13T15:00:00.0000000Z", FieldValueForm.StoreDate("2026-11-13T15:00:00Z", previous, German));
            Assert.Equal("2026-11-13T15:00:00.0000000", FieldValueForm.StoreDate("2026-11-13T15:00:00", previous, German));
            Assert.Equal(previous, FieldValueForm.StoreDate("13.11.2026", previous, German));
            Assert.Equal(previous, FieldValueForm.StoreDate("2026-11-13", previous, German));
        }

        /// <summary>
        /// A time separator marks a submit with time.
        /// </summary>
        [Theory]
        [InlineData("2026-11-13T15:00:00Z", true)]
        [InlineData("13.11.2026 15:00", true)]
        [InlineData("13.11.2026", false)]
        [InlineData("2026-11-13", false)]
        [InlineData(null, false)]
        public void HasTime_FindsTheTimeSeparator(string value, bool expected)
        {
            Assert.Equal(expected, FieldValueForm.HasTime(value));
        }
    }
}
