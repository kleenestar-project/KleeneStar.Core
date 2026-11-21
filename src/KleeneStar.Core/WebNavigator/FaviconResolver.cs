using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KleeneStar.Core.WebNavigator
{
    /// <summary>
    /// Determines the favicon of the site a navigator link points at.
    /// </summary>
    /// <remarks>
    /// The address comes from an operator through the settings page, and resolving it makes this
    /// server issue an outbound request to a target the operator chose. That turns the feature into
    /// a request forgery primitive unless the target is constrained, so every hop is resolved to its
    /// addresses and rejected when it points into the host itself or a private network, redirects
    /// are followed manually so each new hop passes the same check, and the response is bounded in
    /// both time and size. A resolution that cannot be completed returns <c>null</c> rather than
    /// throwing, because the caller keeps its generated icon in that case and a link must never fail
    /// to save because its icon could not be determined.
    /// </remarks>
    public static partial class FaviconResolver
    {
        /// <summary>
        /// The overall time budget for one resolution, kept short because it is spent inside the
        /// request that saves the link.
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The maximum number of bytes read from the target document. The icon declarations live in
        /// the head, so a partial read is enough and an endless response cannot exhaust memory.
        /// </summary>
        private const int MaxDocumentBytes = 256 * 1024;

        /// <summary>
        /// The maximum number of redirects followed, each re-validated before it is requested.
        /// </summary>
        private const int MaxRedirects = 3;

        /// <summary>
        /// The maximum length of the resulting address, matching the length of the icon column. A
        /// longer address is discarded instead of being persisted truncated and thus broken.
        /// </summary>
        private const int MaxAddressLength = 256;

        private static readonly HttpClient _client = CreateClient();

        /// <summary>
        /// Determines the favicon address of the site the specified link address points at.
        /// </summary>
        /// <param name="address">The address configured on the navigator link.</param>
        /// <param name="cancellationToken">
        /// A token that propagates notification that the operation should be cancelled.
        /// </param>
        /// <returns>
        /// The absolute address of the favicon, or <c>null</c> when it could not be determined.
        /// </returns>
        public static async Task<string> ResolveAsync(string address, CancellationToken cancellationToken = default)
        {
            var normalized = NavigatorLinkAddress.Normalize(address);

            // an internal route is served by this host, so there is nothing to fetch: the caller
            // resolves it against the application itself
            if (normalized is null || NavigatorLinkAddress.IsInternal(normalized))
            {
                return null;
            }

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var target)
                || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                budget.CancelAfter(Timeout);

                var document = await FetchAsync(target, budget.Token);

                var candidate = document.Html is null
                    ? null
                    : ParseIconHref(document.Html, document.Location);

                // a site that declares no icon still commonly serves the well known location, so it
                // is probed before giving up
                candidate ??= await ProbeDefaultAsync(document.Location, budget.Token);

                return candidate is not null && candidate.Length <= MaxAddressLength
                    ? candidate
                    : null;
            }
            catch (Exception)
            {
                // an unreachable, slow or malformed target must not fail the save
                return null;
            }
        }

        /// <summary>
        /// Fetches the target document, following redirects manually so every hop is validated.
        /// </summary>
        /// <param name="target">The target address.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The document text and the address it was finally served from.</returns>
        private static async Task<(string Html, Uri Location)> FetchAsync(Uri target, CancellationToken cancellationToken)
        {
            var location = target;

            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                if (!await IsPubliclyRoutableAsync(location, cancellationToken))
                {
                    return (null, location);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, location);
                using var response = await _client.SendAsync
                (
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
                {
                    location = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(location, response.Headers.Location);

                    if (location.Scheme != Uri.UriSchemeHttp && location.Scheme != Uri.UriSchemeHttps)
                    {
                        return (null, target);
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return (null, location);
                }

                return (await ReadBoundedAsync(response, cancellationToken), location);
            }

            return (null, location);
        }

        /// <summary>
        /// Probes the well known favicon location of the specified site.
        /// </summary>
        /// <param name="location">The address of the site.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The address when it serves an image; otherwise <c>null</c>.</returns>
        private static async Task<string> ProbeDefaultAsync(Uri location, CancellationToken cancellationToken)
        {
            try
            {
                var candidate = new Uri(location, "/favicon.ico");

                if (!await IsPubliclyRoutableAsync(candidate, cancellationToken))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Head, candidate);
                using var response = await _client.SendAsync(request, cancellationToken);

                var mediaType = response.Content?.Headers?.ContentType?.MediaType;

                return response.IsSuccessStatusCode
                    && mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
                    ? candidate.ToString()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the icon address declared in the specified document.
        /// </summary>
        /// <remarks>
        /// Declarations are ranked so an explicitly sized icon wins over an unsized one and a plain
        /// icon over a touch icon, which keeps the choice stable on sites that declare several.
        /// </remarks>
        /// <param name="html">The document text.</param>
        /// <param name="location">The address the document was served from.</param>
        /// <returns>The absolute icon address, or <c>null</c> when none is declared.</returns>
        public static string ParseIconHref(string html, Uri location)
        {
            if (string.IsNullOrEmpty(html))
            {
                return null;
            }

            var ranked = new List<(int Rank, string Href)>();

            foreach (Match tag in LinkTagRegex().Matches(html))
            {
                var rel = AttributeRegex("rel").Match(tag.Value);
                var href = AttributeRegex("href").Match(tag.Value);

                if (!rel.Success || !href.Success)
                {
                    continue;
                }

                var relations = AttributeValue(rel)
                    .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.ToLowerInvariant())
                    .ToArray();

                var rank = relations.Contains("icon") ? 2
                    : relations.Contains("apple-touch-icon") ? 1
                    : 0;

                if (rank == 0)
                {
                    continue;
                }

                if (AttributeRegex("sizes").Match(tag.Value).Success)
                {
                    rank += 2;
                }

                ranked.Add((rank, WebUtility.HtmlDecode(AttributeValue(href)).Trim()));
            }

            foreach (var (_, href) in ranked.OrderByDescending(x => x.Rank))
            {
                if (href.Length > 0
                    && !href.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    && Uri.TryCreate(location, href, out var absolute)
                    && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                {
                    return absolute.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether every address the specified host resolves to lies outside this host
        /// and outside the private networks it can reach.
        /// </summary>
        /// <remarks>
        /// The check runs per hop rather than once, because a redirect is a fresh target chosen by
        /// the remote side. It requires all resolved addresses to be routable, so a name that
        /// resolves to both a public and an internal address cannot be used to slip through.
        /// </remarks>
        /// <param name="uri">The address to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><c>true</c> when the address may be requested; otherwise <c>false</c>.</returns>
        private static async Task<bool> IsPubliclyRoutableAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                IPAddress[] addresses;

                if (IPAddress.TryParse(uri.Host, out var literal))
                {
                    addresses = [literal];
                }
                else
                {
                    addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
                }

                return addresses.Length > 0 && addresses.All(IsPubliclyRoutable);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified address lies outside the ranges that would let a target
        /// reach back into this host or its network.
        /// </summary>
        /// <param name="address">The address to classify.</param>
        /// <returns><c>true</c> when the address is publicly routable; otherwise <c>false</c>.</returns>
        public static bool IsPubliclyRoutable(IPAddress address)
        {
            if (address is null
                || IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();

                return !(b[0] == 10                                     // 10.0.0.0/8
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)        // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                     // 192.168.0.0/16
                    || (b[0] == 169 && b[1] == 254)                     // 169.254.0.0/16 link local
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)       // 100.64.0.0/10 carrier grade
                    || b[0] == 127                                      // 127.0.0.0/8
                    || b[0] == 0                                        // 0.0.0.0/8
                    || b[0] >= 224);                                    // multicast and reserved
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return !(address.IsIPv6LinkLocal
                    || address.IsIPv6SiteLocal
                    || address.IsIPv6Multicast
                    || address.IsIPv6UniqueLocal);
            }

            return false;
        }

        /// <summary>
        /// Reads the response content up to the configured bound.
        /// </summary>
        /// <param name="response">The response to read.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The decoded text.</returns>
        private static async Task<string> ReadBoundedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var buffer = new byte[MaxDocumentBytes];
            var total = 0;

            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);

                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return System.Text.Encoding.UTF8.GetString(buffer, 0, total);
        }

        /// <summary>
        /// Determines whether the specified status code denotes a redirect.
        /// </summary>
        /// <param name="status">The status code.</param>
        /// <returns><c>true</c> when the status code is a redirect; otherwise <c>false</c>.</returns>
        private static bool IsRedirect(HttpStatusCode status)
        {
            return status is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Found
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;
        }

        /// <summary>
        /// Creates the client used for the outbound requests.
        /// </summary>
        /// <returns>The client.</returns>
        private static HttpClient CreateClient()
        {
            // redirects are followed by hand so each hop can be validated, therefore the handler
            // must not follow them on its own
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(2),
                AutomaticDecompression = DecompressionMethods.All
            };

            var client = new HttpClient(handler)
            {
                Timeout = Timeout
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("KleeneStar");

            return client;
        }

        /// <summary>
        /// Matches a link element of the document head.
        /// </summary>
        [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex LinkTagRegex();

        /// <summary>
        /// Creates a regular expression matching the specified attribute of a tag.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The regular expression.</returns>
        private static Regex AttributeRegex(string name)
        {
            return new Regex($@"\b{name}\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Returns the value of a matched attribute regardless of how it was quoted.
        /// </summary>
        /// <remarks>
        /// The pattern offers one group per quoting style, so the value has to be taken from
        /// whichever of them participated in the match; reading a fixed group would yield an empty
        /// value for every attribute that is single quoted or unquoted.
        /// </remarks>
        /// <param name="match">The attribute match.</param>
        /// <returns>The attribute value, or an empty string when none participated.</returns>
        private static string AttributeValue(Match match)
        {
            for (var group = 1; group <= 3; group++)
            {
                if (match.Groups[group].Success)
                {
                    return match.Groups[group].Value;
                }
            }

            return string.Empty;
        }
    }
}
