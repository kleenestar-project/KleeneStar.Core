using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// The values an appearance adjustment may take, and the one place that reduces a
    /// submitted value to one of them.
    /// </summary>
    /// <remarks>
    /// Every value ends up inside a stylesheet (<see cref="AppearanceStyle"/>), so nothing is
    /// passed through as written: an accent is accepted only as <c>#rrggbb</c>, a font size
    /// only from <see cref="FontScales"/>, a density only as <see cref="Compact"/>. Anything
    /// else reads as "no choice", which is the theme's own value.
    /// </remarks>
    public static class AppearanceOptions
    {
        /// <summary>
        /// The font size every theme is drawn in, in percent.
        /// </summary>
        public const int DefaultFontScale = 100;

        /// <summary>
        /// The density that tightens the line height (and with it every row).
        /// </summary>
        public const string Compact = "compact";

        /// <summary>
        /// The density of the theme, as the form offers it; stored as no choice.
        /// </summary>
        public const string Comfortable = "comfortable";

        /// <summary>
        /// The form's answer for "keep the theme's accent color".
        /// </summary>
        public const string AccentTheme = "theme";

        /// <summary>
        /// The form's answer for "use the color picked beside it".
        /// </summary>
        public const string AccentCustom = "custom";

        /// <summary>
        /// The color the accent picker starts with when no accent was chosen yet: the primary
        /// color of the framework palette, so the first pick starts from what the page shows.
        /// </summary>
        public const string DefaultAccent = "#0d6efd";

        /// <summary>
        /// Gets the font sizes offered, in percent.
        /// </summary>
        public static IReadOnlyList<int> FontScales { get; } = [90, 100, 110, 125];

        /// <summary>
        /// Reduces a submitted accent color to <c>#rrggbb</c> in lower case.
        /// </summary>
        /// <param name="value">The submitted value (<c>#rgb</c> and <c>#rrggbb</c> are accepted).</param>
        /// <returns>The normalized color, or <see langword="null"/> when the value is no color.</returns>
        public static string NormalizeAccent(string value)
        {
            var text = value?.Trim();

            if (string.IsNullOrEmpty(text) || text[0] != '#')
            {
                return null;
            }

            var hex = text[1..];

            if (hex.Length == 3)
            {
                hex = string.Concat(hex.Select(x => new string(x, 2)));
            }

            if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
            {
                return null;
            }

            return "#" + hex.ToLowerInvariant();
        }

        /// <summary>
        /// Reduces a submitted font size to one of <see cref="FontScales"/>.
        /// </summary>
        /// <param name="value">The submitted percentage.</param>
        /// <returns>The size, or <see langword="null"/> for the default or a size not offered.</returns>
        public static int? NormalizeFontScale(string value)
        {
            var text = value?.Trim().TrimEnd('%');

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale))
            {
                return null;
            }

            return FontScales.Contains(scale) && scale != DefaultFontScale ? scale : null;
        }

        /// <summary>
        /// Reduces a submitted density to <see cref="Compact"/> or no choice.
        /// </summary>
        /// <param name="value">The submitted density.</param>
        /// <returns><see cref="Compact"/>, or <see langword="null"/>.</returns>
        public static string NormalizeDensity(string value)
        {
            return string.Equals(value?.Trim(), Compact, StringComparison.OrdinalIgnoreCase) ? Compact : null;
        }

        /// <summary>
        /// Reads a submitted flag the way a form sends one: a checkbox posts <c>on</c>, a
        /// selection <c>true</c>/<c>yes</c>/<c>1</c>.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <returns>Whether the flag is set.</returns>
        public static bool ReadFlag(string value)
        {
            return value?.Trim().ToLowerInvariant() is "true" or "on" or "yes" or "1";
        }
    }
}
