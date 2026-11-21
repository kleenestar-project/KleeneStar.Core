using System;
using System.Text.Json.Serialization;
using WebExpress.WebIndex;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// The appearance an identity chose for itself: the color theme, light or dark, and the
    /// adjustments laid over whichever theme is in use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every choice is optional, and <see langword="null"/> (or the default of a flag) means
    /// "as the installation ships it": no theme falls back to the application's default theme,
    /// no scheme lets the theme decide, no accent keeps the theme's primary color. A record
    /// without any choice is therefore the same as no record, and is not kept
    /// (<see cref="IsEmpty"/>).
    /// </para>
    /// <para>
    /// It is stored as one JSON value in the identity's key/value preferences
    /// (<c>UserSession</c>, scope <see cref="WebManager.IAppearanceManager.Scope"/>), so the
    /// page renders it with one read and the model needs no table of its own. The record is
    /// also the item of the profile's appearance form (<see cref="IIndexItem"/>); its
    /// <see cref="Id"/> is the owner's identity id.
    /// </para>
    /// </remarks>
    public sealed class AppearancePreference : IIndexItem
    {
        /// <summary>
        /// Gets or sets the identity that owns the record.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the id of the chosen theme (the lower-cased type name WebExpress assigns),
        /// or <see langword="null"/> for the application default.
        /// </summary>
        public string Theme { get; set; }

        /// <summary>
        /// Gets or sets "light" or "dark", or <see langword="null"/> to follow the theme.
        /// </summary>
        public string ColorScheme { get; set; }

        /// <summary>
        /// Gets or sets the accent color as <c>#rrggbb</c>, or <see langword="null"/> for the
        /// theme's own primary color.
        /// </summary>
        public string AccentColor { get; set; }

        /// <summary>
        /// Gets or sets the font size in percent of the theme's (one of
        /// <see cref="AppearanceOptions.FontScales"/>), or <see langword="null"/> for 100 %.
        /// </summary>
        public int? FontScale { get; set; }

        /// <summary>
        /// Gets or sets the density (<see cref="AppearanceOptions.Compact"/>), or
        /// <see langword="null"/> for the theme's spacing.
        /// </summary>
        public string Density { get; set; }

        /// <summary>
        /// Gets or sets whether animations and transitions are switched off regardless of the
        /// operating system's setting.
        /// </summary>
        public bool ReduceMotion { get; set; }

        /// <summary>
        /// Gets whether the record holds no choice at all.
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Theme)
            && string.IsNullOrWhiteSpace(ColorScheme)
            && !HasAdjustments;

        /// <summary>
        /// Gets whether the record changes anything beyond the theme and the scheme - that is,
        /// whether a page needs the override stylesheet of <see cref="AppearanceStyle"/>.
        /// </summary>
        [JsonIgnore]
        public bool HasAdjustments => !string.IsNullOrWhiteSpace(AccentColor)
            || (FontScale.HasValue && FontScale.Value != AppearanceOptions.DefaultFontScale)
            || !string.IsNullOrWhiteSpace(Density)
            || ReduceMotion;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public AppearancePreference()
        {
        }

        /// <summary>
        /// Initializes a new, empty record of an identity.
        /// </summary>
        /// <param name="identityId">The owner of the record.</param>
        public AppearancePreference(Guid identityId)
        {
            Id = identityId;
        }

        /// <summary>
        /// Returns a copy of the record, so a change can be prepared without touching a record
        /// another caller of the same request still reads.
        /// </summary>
        /// <returns>The copy.</returns>
        public AppearancePreference Copy()
        {
            return new AppearancePreference(Id)
            {
                Theme = Theme,
                ColorScheme = ColorScheme,
                AccentColor = AccentColor,
                FontScale = FontScale,
                Density = Density,
                ReduceMotion = ReduceMotion
            };
        }
    }
}
