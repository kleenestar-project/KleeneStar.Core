using System;
using System.Globalization;
using System.Text.Json.Serialization;
using WebExpress.WebIndex;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// The caller's adjustments as the appearance form of the profile answers them
    /// (<see cref="WWW.Api._1_.Appearance.Index"/>).
    /// </summary>
    public sealed class AppearanceForm : IIndexItem
    {
        /// <summary>
        /// Gets or sets the caller's identity id.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets <see cref="AppearanceOptions.AccentTheme"/> or
        /// <see cref="AppearanceOptions.AccentCustom"/>.
        /// </summary>
        public string AccentMode { get; set; }

        /// <summary>
        /// Gets or sets the custom accent color; the picker's starting color without one.
        /// </summary>
        public string AccentColor { get; set; }

        /// <summary>
        /// Gets or sets the font size in percent.
        /// </summary>
        public string FontScale { get; set; }

        /// <summary>
        /// Gets or sets <see cref="AppearanceOptions.Comfortable"/> or
        /// <see cref="AppearanceOptions.Compact"/>.
        /// </summary>
        public string Density { get; set; }

        /// <summary>
        /// Gets or sets "true" or "false".
        /// </summary>
        public string ReduceMotion { get; set; }

        /// <summary>
        /// Projects a stored record onto the form's answers.
        /// </summary>
        /// <param name="identityId">The caller.</param>
        /// <param name="preference">The stored record.</param>
        /// <returns>The answers.</returns>
        public static AppearanceForm From(Guid identityId, AppearancePreference preference)
        {
            var accent = AppearanceOptions.NormalizeAccent(preference?.AccentColor);

            return new AppearanceForm
            {
                Id = identityId,
                AccentMode = accent is null ? AppearanceOptions.AccentTheme : AppearanceOptions.AccentCustom,
                AccentColor = accent ?? AppearanceOptions.DefaultAccent,
                FontScale = (preference?.FontScale ?? AppearanceOptions.DefaultFontScale).ToString(CultureInfo.InvariantCulture),
                Density = AppearanceOptions.NormalizeDensity(preference?.Density) ?? AppearanceOptions.Comfortable,
                ReduceMotion = (preference?.ReduceMotion ?? false) ? "true" : "false"
            };
        }
    }
}
