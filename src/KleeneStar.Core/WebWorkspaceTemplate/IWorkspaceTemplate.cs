using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// A workspace template: the shape a workspace is created in - what it is for, and which
    /// classes it starts with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A template is a <b>class in a plugin</b>, not a row in a table. That is the whole point of
    /// it: what a service desk workspace consists of is knowledge somebody wrote down once, and
    /// it belongs where the rest of that knowledge lives - in an assembly that can be installed,
    /// versioned and removed - rather than in the installation's own data, where it would have to
    /// be seeded, migrated and kept in step with every deployment by hand.
    /// </para>
    /// <para>
    /// It is therefore also read-only at runtime: the <see cref="WebManager.IWorkspaceTemplateManager"/>
    /// discovers implementations and hands them out, and nothing writes them back. What an
    /// administrator changes is the workspace the template produced, never the template.
    /// </para>
    /// <para>
    /// Implementations must be public, sealed and carry a public parameterless constructor - the
    /// manager instantiates them by reflection, the way the framework instantiates fragments.
    /// </para>
    /// </remarks>
    public interface IWorkspaceTemplate
    {
        /// <summary>
        /// Gets the stable key of the template, unique across every plugin.
        /// </summary>
        /// <remarks>
        /// It is what a created workspace records itself as coming from, so it has to outlive
        /// renames of the class. Lower case, dotted, prefixed by the plugin it ships in - e.g.
        /// <c>kleenestar.templates.servicedesk</c>.
        /// </remarks>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the template's display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the internationalization key of the sentence that says what a workspace built
        /// from this template is for. It is what an administrator picks the template by, so it
        /// names the work rather than the contents.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon of the template, and of the workspace it creates.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the suggested key of the workspace, e.g. <c>SD</c>. It is a proposal the wizard
        /// fills the key field with, not a constraint: the administrator may overwrite it, and a
        /// second workspace from the same template needs a different one anyway.
        /// </summary>
        string SuggestedKey { get; }

        /// <summary>
        /// Gets the internationalization keys of the categories the workspace is filed under.
        /// </summary>
        IEnumerable<string> Categories { get; }

        /// <summary>
        /// Gets the display order among the templates offered. Lower values are offered first;
        /// templates of equal order are ordered by their name.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the classes a workspace created from this template starts with.
        /// </summary>
        /// <remarks>
        /// They are what makes a template worth choosing: an empty workspace is one click away
        /// either way, and what takes an afternoon is deciding that a service desk needs a
        /// ticket, an incident, a problem and a change, and that two of them are visible to
        /// customers.
        /// </remarks>
        IEnumerable<WorkspaceTemplateClass> Classes { get; }
    }
}
