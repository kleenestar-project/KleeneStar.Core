using System;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One registered workspace template, together with where it came from.
    /// </summary>
    /// <remarks>
    /// The manager hands out contexts rather than the templates themselves, for the same reason
    /// the framework's fragment manager does: what a caller needs to know about a discovered
    /// element is not only what it says, but which plugin said it - to name the source in the
    /// user interface, and to drop the registration again when that plugin is removed.
    /// </remarks>
    public interface IWorkspaceTemplateContext
    {
        /// <summary>
        /// Gets the plugin the template was discovered in.
        /// </summary>
        IPluginContext PluginContext { get; }

        /// <summary>
        /// Gets the implementing type. It is the identity of the registration; the template's
        /// own <see cref="IWorkspaceTemplate.Key"/> is the identity of what it describes.
        /// </summary>
        Type TemplateType { get; }

        /// <summary>
        /// Gets the template itself.
        /// </summary>
        IWorkspaceTemplate Template { get; }
    }
}
