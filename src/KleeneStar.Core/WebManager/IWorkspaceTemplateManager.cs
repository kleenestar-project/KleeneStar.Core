using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the workspace templates the installed plugins define.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is modelled on the framework's <c>FragmentManager</c> and works the same way: it
    /// listens to the plugin manager, scans the assembly of every plugin that arrives for types
    /// implementing <see cref="IWorkspaceTemplate"/>, keeps one registration per type, and drops
    /// the registrations of a plugin again when that plugin is removed. Nothing is written back;
    /// a template is code, and the catalogue is only ever as long as the set of installed
    /// plugins says.
    /// </para>
    /// <para>
    /// Unlike a fragment, a template is <b>not</b> bound to an application. A fragment is a piece
    /// of one particular page and only means something inside the application that page belongs
    /// to; a workspace template is a description of a workspace, and a workspace is the
    /// installation's, not an application's.
    /// </para>
    /// </remarks>
    public interface IWorkspaceTemplateManager : IComponentManager
    {
        /// <summary>
        /// Raised after a template has been registered.
        /// </summary>
        event EventHandler<IWorkspaceTemplateContext> AddWorkspaceTemplate;

        /// <summary>
        /// Raised after a template has been dropped, because the plugin that defined it was
        /// removed.
        /// </summary>
        event EventHandler<IWorkspaceTemplateContext> RemoveWorkspaceTemplate;

        /// <summary>
        /// Raised when a template was applied to a workspace - after its structure, pages and
        /// approval policies, before its default permissions - so a plugin sets up what the core
        /// does not know, such as automation rules (<c>KleeneStar.Templates</c>).
        /// </summary>
        event EventHandler<WebWorkspaceTemplate.WorkspaceTemplateApplication> TemplateApplied;

        /// <summary>
        /// Gets every registered template, ordered by <see cref="IWorkspaceTemplate.Order"/> and
        /// then by key.
        /// </summary>
        IEnumerable<IWorkspaceTemplateContext> WorkspaceTemplates { get; }

        /// <summary>
        /// Returns the templates the supplied plugin defines.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        /// <returns>Its templates, in catalogue order. The collection may be empty.</returns>
        IEnumerable<IWorkspaceTemplateContext> GetWorkspaceTemplates(IPluginContext pluginContext);

        /// <summary>
        /// Returns the template with the supplied key.
        /// </summary>
        /// <param name="key">The stable key of the template. May be null.</param>
        /// <returns>The registration, or <see langword="null"/> when no plugin defines it - which
        /// is the ordinary answer for a workspace whose template has since been uninstalled.</returns>
        IWorkspaceTemplateContext GetWorkspaceTemplate(string key);

        /// <summary>
        /// Sets a workspace up from the supplied template: its classes, the starting views of
        /// its issue and asset overviews, its home page and the post announcing it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the one thing the manager does beyond answering questions, and it is here
        /// rather than at the call site because applying a template is the same act wherever it
        /// is triggered from - the creation wizard today, an import or a scripted setup
        /// tomorrow - and it is the manager that knows what a registration means.
        /// </para>
        /// <para>
        /// It is more than the classes because a workspace that arrives with classes and nothing
        /// else still has to be set up by hand - empty tab strips on both overviews, no page
        /// saying what the place is for, an empty timeline - which is the afternoon the templates
        /// exist to save. Every part is skipped where the workspace already carries it, so a
        /// second application adds what is missing rather than a second set of everything.
        /// </para>
        /// </remarks>
        /// <param name="key">The stable key of the template to apply.</param>
        /// <param name="workspaceId">The workspace to set up.</param>
        /// <param name="identityId">Who is doing this, recorded as the author of the two pages.
        /// Empty when it is not known.</param>
        /// <param name="culture">The language the two pages are written in - the language of
        /// whoever is creating the workspace, because that is who will read them. Null falls back
        /// to the installation's own, which is what a caller with no request behind it has.</param>
        /// <returns>What was created. Every part is empty when the template or the workspace is
        /// unknown, and when the workspace already carries what would be created.</returns>
        WorkspaceTemplateResult Apply(string key, Guid workspaceId, Guid identityId = default, CultureInfo culture = null);
    }
}
