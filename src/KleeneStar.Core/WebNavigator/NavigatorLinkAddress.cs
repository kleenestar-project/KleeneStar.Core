using System.Text.RegularExpressions;

namespace KleeneStar.Core.WebNavigator
{
    /// <summary>
    /// Interprets the address configured on a navigator link.
    /// </summary>
    /// <remarks>
    /// The interpretation is shared by the app navigator, which turns the address into the link
    /// target, and by the favicon resolution, which needs the very same reading to know which site
    /// to ask. Keeping it in one place stops the two from drifting apart and rendering a link that
    /// points somewhere other than the site whose icon is shown.
    /// </remarks>
    public static partial class NavigatorLinkAddress
    {
        /// <summary>
        /// Normalizes a configured address into either an absolute address carrying a scheme or a
        /// server-internal route starting with a slash.
        /// </summary>
        /// <remarks>
        /// An address that already carries a scheme or starts with a slash is unambiguous and is
        /// returned as is. Everything else is classified by whether it looks like a host, that is
        /// whether a dot appears before the first slash: <c>example.com/x</c> becomes an external
        /// address, <c>workspaces</c> a server-internal route. Without this step such a value would
        /// reach the uri parser as a relative path and collapse to the site root, turning the entry
        /// into a link that silently points at <c>/</c>.
        /// </remarks>
        /// <param name="address">The configured address.</param>
        /// <returns>
        /// The normalized address, or <c>null</c> when no address is configured.
        /// </returns>
        public static string Normalize(string address)
        {
            var value = address?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (value.StartsWith('/') || SchemeRegex().IsMatch(value))
            {
                return value;
            }

            var host = value.Split('/', 2)[0];

            return host.Contains('.')
                ? $"https://{value}"
                : $"/{value}";
        }

        /// <summary>
        /// Determines whether the specified normalized address points at this server rather than at
        /// an external system.
        /// </summary>
        /// <param name="normalizedAddress">The normalized address.</param>
        /// <returns>
        /// <c>true</c> if the address is a server-internal route; otherwise <c>false</c>.
        /// </returns>
        public static bool IsInternal(string normalizedAddress)
        {
            return normalizedAddress?.StartsWith('/') == true;
        }

        /// <summary>
        /// Matches an address that already carries a uri scheme, for example <c>https://</c>.
        /// </summary>
        [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.\-]*:")]
        private static partial Regex SchemeRegex();
    }
}
