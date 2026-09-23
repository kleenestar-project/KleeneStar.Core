using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// What applying a workspace template produced.
    /// </summary>
    /// <remarks>
    /// Applying a template is not one act but four - the classes with their structure, the
    /// standard views of the issue and asset overviews, the home page and the opening post - and
    /// each of them is
    /// skipped when the workspace already carries it. A caller that only got a count back could
    /// not tell "the template was already applied" from "the template creates nothing", and a
    /// test could not say which half of a retry did nothing. Each part is therefore reported
    /// separately, and every one of them is empty on a second application.
    /// </remarks>
    public sealed class WorkspaceTemplateResult
    {
        /// <summary>
        /// Gets the empty result, answered when the template or the workspace is unknown.
        /// </summary>
        public static WorkspaceTemplateResult Empty { get; } = new();

        /// <summary>
        /// Gets the classes that were created. Empty when the workspace already carried a class
        /// of every name the template names.
        /// </summary>
        public IReadOnlyList<Class> Classes { get; init; } = [];

        /// <summary>
        /// Gets the fields created in those classes. The structure below is written only into
        /// a class this application created - a class the workspace already carried is left as
        /// its administrator shaped it.
        /// </summary>
        public IReadOnlyList<Field> Fields { get; init; } = [];

        /// <summary>
        /// Gets the priorities created in those classes.
        /// </summary>
        public IReadOnlyList<Priority> Priorities { get; init; } = [];

        /// <summary>
        /// Gets the workflow states created in those classes.
        /// </summary>
        public IReadOnlyList<Status> Statuses { get; init; } = [];

        /// <summary>
        /// Gets the workflows created in those classes.
        /// </summary>
        public IReadOnlyList<Workflow> Workflows { get; init; } = [];

        /// <summary>
        /// Gets the forms created in those classes.
        /// </summary>
        public IReadOnlyList<Form> Forms { get; init; } = [];

        /// <summary>
        /// Gets the calendars created in those classes.
        /// </summary>
        public IReadOnlyList<Calendar> Calendars { get; init; } = [];

        /// <summary>
        /// Gets the service-level agreements created in those classes.
        /// </summary>
        public IReadOnlyList<SlaPolicy> Slas { get; init; } = [];

        /// <summary>
        /// Gets the overview tabs that were created, across both object kinds.
        /// </summary>
        public IReadOnlyList<ObjectView> Views { get; init; } = [];

        /// <summary>
        /// Gets the home page that was written, or <see langword="null"/> when the workspace
        /// already had a document.
        /// </summary>
        public Model.Entities.Object Home { get; init; }

        /// <summary>
        /// Gets the post announcing the workspace, or <see langword="null"/> when the workspace
        /// already had one.
        /// </summary>
        public Model.Entities.Object OpeningPost { get; init; }
    }
}
