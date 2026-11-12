using KleeneStar.Core.WebTheme;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace KleeneStar.Core.Test.WebTheme
{
    /// <summary>
    /// Tests <see cref="AppearanceStyle"/> and <see cref="AppearanceOptions"/>: what a reader's
    /// adjustments turn into, and that nothing reaches the stylesheet that is not one of the
    /// values on offer.
    /// </summary>
    public class UnitTestAppearanceStyle
    {
        /// <summary>
        /// A record without adjustments writes no stylesheet - theme and mode are applied by
        /// the framework, not by an override.
        /// </summary>
        [Fact]
        public void NoAdjustmentWritesNothing()
        {
            Assert.Null(AppearanceStyle.Build(null));
            Assert.Null(AppearanceStyle.Build(new AppearancePreference()));
            Assert.Null(AppearanceStyle.Build(new AppearancePreference { Theme = "x", ColorScheme = "dark", FontScale = 100 }));
        }

        /// <summary>
        /// An accent redefines the primary, link and highlight tokens for light and for dark,
        /// the dark block last, because on a dark page the root matches both with equal weight.
        /// </summary>
        [Fact]
        public void AccentRedefinesTheTokensOfBothSchemes()
        {
            var css = AppearanceStyle.Build(new AppearancePreference { AccentColor = "#FF8800" });

            Assert.NotNull(css);
            Assert.Equal(2, Regex.Matches(css, @"--wx-primary: #ff8800;").Count);
            Assert.Contains("--wx-primary-rgb: 255, 136, 0;", css);
            Assert.Contains("--wx-highlight: #ff8800;", css);
            Assert.True(css.IndexOf(":root, [data-wx-theme=\"light\"]", StringComparison.Ordinal)
                < css.IndexOf("[data-wx-theme=\"dark\"] {", StringComparison.Ordinal));
        }

        /// <summary>
        /// The link color reaches 4.5:1 against the page in both schemes, even for an accent
        /// that is far too pale to be read as text.
        /// </summary>
        /// <param name="accent">The accent picked.</param>
        [Theory]
        [InlineData("#ffff00")]
        [InlineData("#d9b4d5")]
        [InlineData("#0000ff")]
        [InlineData("#202020")]
        public void LinksStayReadable(string accent)
        {
            var css = AppearanceStyle.Build(new AppearancePreference { AccentColor = accent });
            var links = Regex.Matches(css, @"--wx-link-color: (#[0-9a-f]{6});").Select(x => x.Groups[1].Value).ToList();

            Assert.Equal(2, links.Count);
            Assert.True(Contrast(links[0], "#ffffff") >= 4.5, $"light link {links[0]} on white");
            Assert.True(Contrast(links[1], "#212529") >= 4.5, $"dark link {links[1]} on the dark page");
        }

        /// <summary>
        /// The text on the accent is whichever ink reads better on it.
        /// </summary>
        [Fact]
        public void ContrastTextFollowsTheAccent()
        {
            Assert.Contains("--wx-primary-contrast: #ffffff;", AppearanceStyle.Build(new AppearancePreference { AccentColor = "#1a237e" }));
            Assert.Contains("--wx-primary-contrast: #212529;", AppearanceStyle.Build(new AppearancePreference { AccentColor = "#ffe082" }));
        }

        /// <summary>
        /// Font size, density and motion each write their own rule.
        /// </summary>
        [Fact]
        public void SizeDensityAndMotionWriteTheirRules()
        {
            var css = AppearanceStyle.Build(new AppearancePreference
            {
                FontScale = 110,
                Density = AppearanceOptions.Compact,
                ReduceMotion = true
            });

            Assert.Contains("html { font-size: 110%; }", css);
            Assert.Contains("--wx-body-line-height: 1.25;", css);
            Assert.Contains("transition-duration: 0.01ms !important;", css);
            Assert.DoesNotContain("--wx-primary", css);
        }

        /// <summary>
        /// A value that is not on offer never reaches the stylesheet.
        /// </summary>
        [Fact]
        public void UnofferedValuesAreDropped()
        {
            Assert.Null(AppearanceStyle.Build(new AppearancePreference
            {
                AccentColor = "red; } body { display: none",
                FontScale = 400,
                Density = "</style><script>"
            }));
        }

        /// <summary>
        /// The submitted values are reduced to the offered ones.
        /// </summary>
        [Fact]
        public void OptionsNormalize()
        {
            Assert.Equal("#aabbcc", AppearanceOptions.NormalizeAccent(" #ABC "));
            Assert.Equal("#a1b2c3", AppearanceOptions.NormalizeAccent("#A1B2C3"));
            Assert.Null(AppearanceOptions.NormalizeAccent("a1b2c3"));
            Assert.Null(AppearanceOptions.NormalizeAccent("#a1b2c"));
            Assert.Null(AppearanceOptions.NormalizeAccent("#ggg"));

            Assert.Equal(125, AppearanceOptions.NormalizeFontScale("125"));
            Assert.Equal(90, AppearanceOptions.NormalizeFontScale("90%"));
            Assert.Null(AppearanceOptions.NormalizeFontScale("100"));
            Assert.Null(AppearanceOptions.NormalizeFontScale("115"));
            Assert.Null(AppearanceOptions.NormalizeFontScale("big"));

            Assert.Equal(AppearanceOptions.Compact, AppearanceOptions.NormalizeDensity("Compact"));
            Assert.Null(AppearanceOptions.NormalizeDensity(AppearanceOptions.Comfortable));

            Assert.True(AppearanceOptions.ReadFlag("on"));
            Assert.True(AppearanceOptions.ReadFlag("true"));
            Assert.False(AppearanceOptions.ReadFlag("false"));
            Assert.False(AppearanceOptions.ReadFlag(null));
        }

        /// <summary>
        /// Computes the WCAG contrast ratio of two <c>#rrggbb</c> colors, independently of the
        /// implementation under test.
        /// </summary>
        /// <param name="a">The first color.</param>
        /// <param name="b">The second color.</param>
        /// <returns>The ratio.</returns>
        private static double Contrast(string a, string b)
        {
            static double Luminance(string hex)
            {
                double Channel(int offset)
                {
                    var c = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;

                    return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
                }

                return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
            }

            var la = Luminance(a);
            var lb = Luminance(b);

            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }
    }
}
