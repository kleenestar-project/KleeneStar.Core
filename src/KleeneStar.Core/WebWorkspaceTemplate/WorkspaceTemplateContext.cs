using System;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// The <see cref="IWorkspaceTemplateContext"/> the manager builds while registering.
    /// </summary>
    internal sealed class WorkspaceTemplateContext : IWorkspaceTemplateContext
    {
        /// <summary>
        /// Gets the plugin the template was discovered in.
        /// </summary>
        public IPluginContext PluginContext { get; init; }

        /// <summary>
        /// Gets the implementing type.
        /// </summary>
        public Type TemplateType { get; init; }

        /// <summary>
        /// Gets the template itself.
        /// </summary>
        public IWorkspaceTemplate Template { get; init; }
    }
}
