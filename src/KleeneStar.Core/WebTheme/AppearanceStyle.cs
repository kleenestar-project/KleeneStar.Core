using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// Turns the adjustments of an <see cref="AppearancePreference"/> into the stylesheet laid
    /// over the active theme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A theme is a set of tokens (<c>--wx-*</c>) every control reads, so an adjustment is a
    /// redefinition of tokens, never a rule for a particular control: the accent redefines the
    /// primary, link and highlight tokens, the font size the root size every <c>rem</c> is
    /// measured in, the density the body line height. That is why the adjustments work on every
    /// theme, including one an installation adds later.
    /// </para>
    /// <para>
    /// The accent is one color, but the tokens it replaces come in families the theme tuned per
    /// scheme - subtle background, subtle border, emphasized text, link. They are derived here by
    /// mixing the accent towards the scheme's paper or ink, and the link color is shifted until
    /// it reaches a contrast of 4.5:1 against the page, because a pale accent the user picked for
    /// buttons would otherwise make every link unreadable.
    /// </para>
    /// <para>
    /// The sheet is emitted after the theme's stylesheet (<see cref="WebFragment.Appearance.AppearanceFragment"/>),
    /// so its selectors - the same the themes use - win by order, not by weight.
    /// </para>
    /// </remarks>
    public static class AppearanceStyle
    {
        /// <summary>
        /// The paper of a light page the derived tokens are measured against.
        /// </summary>
        private static readonly Rgb _lightPaper = new(255, 255, 255);

        /// <summary>
        /// The ink of a light page.
        /// </summary>
        private static readonly Rgb _lightInk = new(33, 37, 41);

        /// <summary>
        /// The paper of a dark page.
        /// </summary>
        private static readonly Rgb _darkPaper = new(33, 37, 41);

        /// <summary>
        /// The ink of a dark page.
        /// </summary>
        private static readonly Rgb _darkInk = new(248, 249, 250);

        /// <summary>
        /// The contrast a link has to reach against the page (WCAG AA for body text).
        /// </summary>
        private const double LinkContrast = 4.5;

        /// <summary>
        /// Builds the stylesheet of the adjustments.
        /// </summary>
        /// <param name="preference">The appearance of the caller.</param>
        /// <returns>The stylesheet, or <see langword="null"/> when nothing is adjusted.</returns>
        public static string Build(AppearancePreference preference)
        {
            if (preference is null || !preference.HasAdjustments)
            {
                return null;
            }

            var css = new StringBuilder();
            var accent = Rgb.Parse(AppearanceOptions.NormalizeAccent(preference.AccentColor));

            if (accent is not null)
            {
                // the light block comes first: on a dark page the root matches both selectors
                // with the same weight, and the later one has to be the dark one
                AppendAccent(css, ":root, [data-wx-theme=\"light\"]", accent.Value, _lightPaper, _lightInk);
                AppendAccent(css, "[data-wx-theme=\"dark\"]", accent.Value, _darkPaper, _darkInk);
            }

            var scale = preference.FontScale;

            if (scale.HasValue && scale.Value != AppearanceOptions.DefaultFontScale
                && AppearanceOptions.FontScales.Contains(scale.Value))
            {
                // every size of the framework is in rem, so the root size scales the whole page
                css.Append("html { font-size: ")
                    .Append(scale.Value.ToString(CultureInfo.InvariantCulture))
                    .Append("%; }\n");
            }

            if (AppearanceOptions.NormalizeDensity(preference.Density) == AppearanceOptions.Compact)
            {
                // the line height is what every row, list entry and input is measured by
                css.Append(":root, [data-wx-theme] { --wx-body-line-height: 1.25; }\n");
            }

            if (preference.ReduceMotion)
            {
                // the same reduction the framework applies under prefers-reduced-motion, for
                // a user who wants it without changing the operating system
                css.Append("*, *::before, *::after { ")
                    .Append("animation-duration: 0.01ms !important; ")
                    .Append("animation-iteration-count: 1 !important; ")
                    .Append("transition-duration: 0.01ms !important; ")
                    .Append("scroll-behavior: auto !important; }\n");
            }

            return css.Length == 0 ? null : css.ToString();
        }

        /// <summary>
        /// Appends the accent tokens of one scheme.
        /// </summary>
        /// <param name="css">The stylesheet being written.</param>
        /// <param name="selector">The selector of the scheme.</param>
        /// <param name="accent">The accent color.</param>
        /// <param name="paper">The page background of the scheme.</param>
        /// <param name="ink">The text color of the scheme.</param>
        private static void AppendAccent(StringBuilder css, string selector, Rgb accent, Rgb paper, Rgb ink)
        {
            var contrast = ContrastOn(accent);
            var link = Readable(accent, paper, ink);
            var hover = link.Mix(ink, 0.3);

            css.Append(selector).Append(" {\n");
            Token(css, "--wx-primary", accent.Hex);
            Token(css, "--wx-primary-rgb", accent.Triple);
            Token(css, "--wx-primary-contrast", contrast.Hex);
            Token(css, "--wx-primary-bg-subtle", accent.Mix(paper, 0.85).Hex);
            Token(css, "--wx-primary-border-subtle", accent.Mix(paper, 0.45).Hex);
            Token(css, "--wx-primary-text-emphasis", accent.Mix(ink, 0.45).Hex);
            Token(css, "--wx-link-color", link.Hex);
            Token(css, "--wx-link-color-rgb", link.Triple);
            Token(css, "--wx-link-hover-color", hover.Hex);
            Token(css, "--wx-highlight", accent.Hex);
            Token(css, "--wx-highlight-rgb", accent.Triple);
            Token(css, "--wx-highlight-contrast", contrast.Hex);
            css.Append("}\n");
        }

        /// <summary>
        /// Appends one token declaration.
        /// </summary>
        /// <param name="css">The stylesheet being written.</param>
        /// <param name="name">The token.</param>
        /// <param name="value">Its value.</param>
        private static void Token(StringBuilder css, string name, string value)
        {
            css.Append("    ").Append(name).Append(": ").Append(value).Append(";\n");
        }

        /// <summary>
        /// Returns the text color that reads on the accent: the darker or the lighter ink,
        /// whichever has the higher contrast.
        /// </summary>
        /// <param name="accent">The accent color.</param>
        /// <returns>The text color.</returns>
        internal static Rgb ContrastOn(Rgb accent)
        {
            return Contrast(accent, _lightInk) >= Contrast(accent, _lightPaper) ? _lightInk : _lightPaper;
        }

        /// <summary>
        /// Shifts the accent towards the ink until it reads on the paper.
        /// </summary>
        /// <param name="accent">The accent color.</param>
        /// <param name="paper">The page background.</param>
        /// <param name="ink">The text color the accent is moved towards.</param>
        /// <returns>The readable color; the accent itself when it already reads.</returns>
        internal static Rgb Readable(Rgb accent, Rgb paper, Rgb ink)
        {
            for (var step = 0; step <= 10; step++)
            {
                var candidate = accent.Mix(ink, step / 10.0);

                if (Contrast(candidate, paper) >= LinkContrast)
                {
                    return candidate;
                }
            }

            return ink;
        }

        /// <summary>
        /// Returns the WCAG contrast ratio of two colors.
        /// </summary>
        /// <param name="a">The first color.</param>
        /// <param name="b">The second color.</param>
        /// <returns>The ratio, from 1 to 21.</returns>
        internal static double Contrast(Rgb a, Rgb b)
        {
            var la = a.Luminance;
            var lb = b.Luminance;

            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>
        /// A color of the sRGB space.
        /// </summary>
        /// <param name="R">The red channel, 0 to 255.</param>
        /// <param name="G">The green channel, 0 to 255.</param>
        /// <param name="B">The blue channel, 0 to 255.</param>
        internal readonly record struct Rgb(int R, int G, int B)
        {
            /// <summary>
            /// Gets the color as <c>#rrggbb</c>.
            /// </summary>
            public string Hex => $"#{R:x2}{G:x2}{B:x2}";

            /// <summary>
            /// Gets the channels as the framework's <c>*-rgb</c> tokens write them.
            /// </summary>
            public string Triple => $"{R}, {G}, {B}";

            /// <summary>
            /// Gets the relative luminance (WCAG 2).
            /// </summary>
            public double Luminance => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

            /// <summary>
            /// Mixes the color with another.
            /// </summary>
            /// <param name="other">The color mixed in.</param>
            /// <param name="amount">The share of <paramref name="other"/>, 0 to 1.</param>
            /// <returns>The mixed color.</returns>
            public Rgb Mix(Rgb other, double amount)
            {
                return new Rgb
                (
                    Channel(R + (other.R - R) * amount),
                    Channel(G + (other.G - G) * amount),
                    Channel(B + (other.B - B) * amount)
                );
            }

            /// <summary>
            /// Reads a normalized <c>#rrggbb</c> color.
            /// </summary>
            /// <param name="hex">The color, or <see langword="null"/>.</param>
            /// <returns>The color, or <see langword="null"/>.</returns>
            public static Rgb? Parse(string hex)
            {
                if (hex is null || hex.Length != 7)
                {
                    return null;
                }

                return new Rgb
                (
                    int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                );
            }

            /// <summary>
            /// Rounds and clamps a channel.
            /// </summary>
            /// <param name="value">The channel value.</param>
            /// <returns>The channel, 0 to 255.</returns>
            private static int Channel(double value)
            {
                return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
            }

            /// <summary>
            /// Linearizes an sRGB channel.
            /// </summary>
            /// <param name="channel">The channel, 0 to 255.</param>
            /// <returns>The linear value, 0 to 1.</returns>
            private static double Linear(int channel)
            {
                var c = channel / 255.0;

                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
        }
    }
}
