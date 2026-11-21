using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebResource;

namespace KleeneStar.Core.WWW.Assets.Img
{
    /// <summary>
    /// Serves the application's embedded image assets (the logo and the entity icons) at
    /// <c>/assets/img/{filename}</c>.
    /// </summary>
    /// <remarks>
    /// The image endpoint is authored as a WWW resource under the <c>/assets</c> subtree - beside
    /// <see cref="Icons.Index"/> - because that subtree is owned by the application's own asset pages
    /// (the asset-kind overview and the icon resource) and therefore shadows the framework's
    /// auto-registered embedded-asset endpoints at <c>/assets/img/…</c>: a core-plugin asset mounts
    /// under <c>/{app}/assets/…</c> (no plugin-id segment), which those pages occupy. Serving the
    /// images through a resource in the same subtree makes <c>/assets/img/…</c> resolve, so the logo
    /// (<c>kleenestar.svg</c>) and the entity icons (<c>class.svg</c>, <c>dashboard.svg</c>, …) load
    /// instead of returning 404.
    /// </remarks>
    [IncludeSubPaths(true)]
    public sealed class Index : IResource
    {
        /// <summary>
        /// The embedded-resource name prefix under which the image assets are compiled, using the
        /// project's <c>Assets</c> logical-name convention.
        /// </summary>
        private const string ResourcePrefix = "KleeneStar.Core.Assets.img.";

        private readonly IResourceContext _resourceContext;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="resourceContext">The resource context.</param>
        public Index(IResourceContext resourceContext)
        {
            _resourceContext = resourceContext;
        }

        /// <summary>
        /// Processing of the resource: resolves the requested file name to an embedded image resource
        /// and returns its bytes with the matching content type.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public IResponse Process(IRequest request)
        {
            var route = _resourceContext.Route;
            var filename = request.Uri.PathSegments.Skip(route.PathSegments.Count()).LastOrDefault()?.ToString();

            // only a bare file name is valid; a traversal segment or an empty tail is rejected
            if (string.IsNullOrWhiteSpace(filename) || filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            {
                return new ResponseBadRequest();
            }

            var assembly = typeof(Index).Assembly;

            // the build embeds nested assets with directory separators in the resource name; normalise
            // every separator to a dot so the lookup matches regardless of the build's separator.
            var expected = ResourcePrefix + filename;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.Replace('\\', '.').Replace('/', '.')
                    .Equals(expected, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                return new ResponseNotFound();
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                return new ResponseNotFound();
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var data = memory.ToArray();

            var response = new ResponseOK();
            response.Header.ContentLength = data.Length;
            response.Header.ContentType = GetMimeType(filename);
            response.Header.CacheControl = "public, max-age=31536000, immutable";
            response.Content = data;

            return response;
        }

        /// <summary>
        /// Returns the MIME type associated with the specified file name based on its extension.
        /// </summary>
        /// <param name="filename">The file name whose MIME type is inferred from its extension.</param>
        /// <returns>The MIME type, or <c>application/octet-stream</c> when the extension is unknown.</returns>
        private static string GetMimeType(string filename)
        {
            var extension = Path.GetExtension(filename)?.ToLowerInvariant();

            return extension switch
            {
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".ico" => "image/x-icon",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}
