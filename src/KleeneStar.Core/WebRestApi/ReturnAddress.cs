using System;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Resolves the address a navigation endpoint redirects to after it acted: the page the
    /// request came from, when that page is on the same host, otherwise a fallback.
    /// </summary>
    /// <remarks>
    /// The workflow transition and the favorite toggle are plain links. On a detail page the
    /// referring page is the detail page; in the preview pane of an overview it is the overview,
    /// so the caller stays on the page the link was clicked on. Only the path and query of the
    /// referring address are used, and only when its host equals the request's
    /// <c>Host</c> header; any other referrer yields the fallback, so the redirect cannot leave
    /// the installation.
    /// </remarks>
    public static class ReturnAddress
    {
        /// <summary>
        /// Returns the referring page of the request, or the fallback.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="fallback">The address used without a usable referrer.</param>
        /// <returns>The redirect target.</returns>
        public static IUri Resolve(IRequest request, IUri fallback)
        {
            var referer = request?.Header?.Referer;
            var host = request?.Header?.Host;

            if (string.IsNullOrWhiteSpace(referer)
                || string.IsNullOrWhiteSpace(host)
                || !Uri.TryCreate(referer, UriKind.Absolute, out var address)
                || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)
                || !string.Equals(address.Authority, host, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            return new UriEndpoint(address.PathAndQuery);
        }
    }
}
