using Microsoft.Extensions.Configuration;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The settings of the object hierarchy, read from the core plugin's own section of the
    /// merged configuration (<c>Plugins:kleenestar.core:Hierarchy</c>).
    /// </summary>
    /// <remarks>
    /// A section that is missing, or silent about a value, keeps the defaults that live as
    /// property initializers here - the same contract <c>DatabaseSettings</c> follows.
    /// </remarks>
    public sealed class HierarchySettings
    {
        /// <summary>
        /// The name of the configuration section below the plugin's own settings.
        /// </summary>
        public const string Section = "Hierarchy";

        /// <summary>
        /// The deepest level an object may sit at, counting a root object as level one. The
        /// limit keeps every walk along the hierarchy - ancestors, breadcrumbs, the cycle check
        /// of a move - bounded.
        /// </summary>
        public int MaxDepth { get; set; } = 32;

        /// <summary>
        /// Gets the effective depth limit: the configured one, never below one level.
        /// </summary>
        public int EffectiveMaxDepth => MaxDepth < 1 ? 1 : MaxDepth;

        /// <summary>
        /// Gets or sets the most objects one recursive operation (archive, delete, clone …) may
        /// act on (<c>Hierarchy:RecursiveLimit</c>, 10,000); a larger subtree is refused.
        /// </summary>
        public int RecursiveLimit { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the most objects a recursive operation runs within the request
        /// (<c>Hierarchy:InlineLimit</c>, 200); a larger one runs in the background and reports
        /// its progress.
        /// </summary>
        public int InlineLimit { get; set; } = 200;

        /// <summary>
        /// Binds the settings from the supplied plugin configuration.
        /// </summary>
        /// <param name="pluginSettings">
        /// The plugin's own configuration section, or <c>null</c> (tests), which keeps the defaults.
        /// </param>
        /// <returns>The settings. Never null.</returns>
        public static HierarchySettings From(IConfiguration pluginSettings)
        {
            var settings = new HierarchySettings();

            pluginSettings?.GetSection(Section).Bind(settings);

            return settings;
        }
    }
}
