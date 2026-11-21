using System;
using System.IO;
using System.Linq;
using System.Text;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Builds the combined inline widget registration script shared by every fully-editable
    /// dashboard control (the standalone dashboard view, and the object/asset Dashboard tab
    /// KPI boards): the app-specific widget types (and their i18n) that live in the embedded
    /// scripts <c>Assets/js/i18n/en.js</c>, <c>Assets/js/i18n/de.js</c> and
    /// <c>Assets/js/widgets/kleenestar.js</c>.
    /// </summary>
    /// <remarks>
    /// The scripts are emitted inline into the page head, in i18n-then-widget order (the i18n
    /// registrations must precede the widget registration that looks them up), rather than
    /// served as static assets: the core plugin's own embedded assets mount under
    /// <c>/{app}/assets/…</c>, a route the application already owns for its workspace-assets
    /// feature, so they are shadowed and never served. Inlining keeps the widgets working
    /// regardless of which page renders the editable board.
    /// </remarks>
    internal static class DashboardWidgetScript
    {
        /// <summary>
        /// The embedded script resources emitted inline, resolved by suffix and concatenated in
        /// this order: the i18n registrations first (so the widget titles and settings labels
        /// resolve), then the widget registration.
        /// </summary>
        private static readonly string[] ScriptResourceSuffixes =
        [
            "js/i18n/en.js",
            "js/i18n/de.js",
            "js/widgets/kleenestar.js"
        ];

        /// <summary>
        /// The combined inline widget script, built once on first use from the embedded
        /// resources. It is language-independent because it registers every shipped language.
        /// </summary>
        private static readonly Lazy<string> Script = new(Build);

        /// <summary>
        /// Gets the combined inline widget registration script, or an empty string when no
        /// resource could be read.
        /// </summary>
        public static string Value => Script.Value;

        /// <summary>
        /// Builds the combined inline widget script by reading and concatenating the embedded
        /// script resources in <see cref="ScriptResourceSuffixes"/> order.
        /// </summary>
        /// <returns>The combined script, or an empty string when no resource could be read.</returns>
        private static string Build()
        {
            var assembly = typeof(DashboardWidgetScript).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            var builder = new StringBuilder();

            foreach (var suffix in ScriptResourceSuffixes)
            {
                var resourceName = resourceNames.FirstOrDefault(name => name.Replace('\\', '/')
                    .EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                if (resourceName is null)
                {
                    continue;
                }

                using var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream is null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream, Encoding.UTF8);
                builder.Append(reader.ReadToEnd()).Append('\n');
            }

            return builder.ToString();
        }
    }
}
