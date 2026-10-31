using System;
using System.IO;
using System.Linq;
using System.Text;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Builds the script that opens a comment composer on its WYSIWYG form, from the embedded
    /// <c>Assets/js/commentcomposer.js</c>.
    /// </summary>
    /// <remarks>
    /// The script is emitted inline into the page head rather than linked as a static asset, for
    /// the same reason the inline cell renderer is (see
    /// the embedded application scripts): the core plugin's own embedded assets
    /// would mount under <c>/{app}/assets/…</c>, a route the application already owns for its
    /// workspace-assets feature, so a link to them answers 404.
    /// <para>
    /// <b>Framework gap.</b> <c>ControlDataCommentComposer</c> always mounts collapsed and offers
    /// no way to say otherwise: neither the control nor its client controller
    /// (<c>webexpress.webapp.CommentComposerCtrl</c>) reads an initial state, and the expansion is
    /// behind the private <c>_expand()</c>. The remedy on the framework side is small - give the
    /// control an <c>Expanded</c> resolver, emit it as <c>data-expanded</c>, have the controller
    /// call its expansion when the attribute says <c>"true"</c>, and let that path skip the focus
    /// it takes on a user-driven expansion. Once that exists, this script and its asset can go and
    /// the fragment sets the resolver instead.
    /// </para>
    /// </remarks>
    internal static class CommentComposerExpandScript
    {
        /// <summary>
        /// The suffix of the embedded script resource, matched against the manifest resource
        /// names with their directory separators normalized.
        /// </summary>
        private const string ScriptResourceSuffix = "js/commentcomposer.js";

        /// <summary>
        /// The class a composer carries to ask the script to open it. It has to match the
        /// constant the script reads.
        /// </summary>
        public const string OptInClass = "ks-comment-composer-open";

        /// <summary>
        /// The inline script, read once on first use.
        /// </summary>
        private static readonly Lazy<string> Script = new(Build);

        /// <summary>
        /// Gets the inline script, or an empty string when the resource could not be read.
        /// </summary>
        public static string Value => Script.Value;

        /// <summary>
        /// Reads the embedded script resource.
        /// </summary>
        /// <returns>The script, or an empty string when the resource could not be read.</returns>
        private static string Build()
        {
            var assembly = typeof(CommentComposerExpandScript).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.Replace('\\', '/')
                    .EndsWith(ScriptResourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
            {
                return string.Empty;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);

            return reader.ReadToEnd();
        }
    }
}
