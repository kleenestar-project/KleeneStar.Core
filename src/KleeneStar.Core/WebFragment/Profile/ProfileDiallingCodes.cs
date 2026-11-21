using System.Collections.Generic;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The international dialling prefixes the profile form offers next to the phone number.
    /// </summary>
    /// <remarks>
    /// A short, curated list rather than the full ITU catalogue: the prefix is a convenience
    /// for the number beside it, and a picker with two hundred entries would cost more to use
    /// than typing the digits. A prefix that is missing can always be written into the number
    /// itself.
    /// </remarks>
    internal static class ProfileDiallingCodes
    {
        /// <summary>
        /// Gets the offered prefixes as (dialling code, label) pairs, the label carrying the
        /// country code so the short entries stay distinguishable in the closed dropdown.
        /// </summary>
        public static IReadOnlyList<(string Code, string Label)> All { get; } =
        [
            ("+49", "DE  +49"),
            ("+43", "AT  +43"),
            ("+41", "CH  +41"),
            ("+31", "NL  +31"),
            ("+32", "BE  +32"),
            ("+33", "FR  +33"),
            ("+34", "ES  +34"),
            ("+39", "IT  +39"),
            ("+44", "GB  +44"),
            ("+45", "DK  +45"),
            ("+46", "SE  +46"),
            ("+47", "NO  +47"),
            ("+48", "PL  +48"),
            ("+351", "PT  +351"),
            ("+353", "IE  +353"),
            ("+358", "FI  +358"),
            ("+420", "CZ  +420"),
            ("+1", "US/CA  +1")
        ];
    }
}
