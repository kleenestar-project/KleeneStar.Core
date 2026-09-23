using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// Collects what <see cref="WorkspaceTemplateStructure"/> writes over all classes of one
    /// application of a template, for the <see cref="WorkspaceTemplateResult"/>.
    /// </summary>
    internal sealed class WorkspaceTemplateStructureResult
    {
        /// <summary>
        /// Gets the fields created.
        /// </summary>
        public List<Field> Fields { get; } = [];

        /// <summary>
        /// Gets the priorities created.
        /// </summary>
        public List<Priority> Priorities { get; } = [];

        /// <summary>
        /// Gets the states created.
        /// </summary>
        public List<Status> Statuses { get; } = [];

        /// <summary>
        /// Gets the workflows created.
        /// </summary>
        public List<Workflow> Workflows { get; } = [];

        /// <summary>
        /// Gets the forms created.
        /// </summary>
        public List<Form> Forms { get; } = [];

        /// <summary>
        /// Gets the calendars created.
        /// </summary>
        public List<Calendar> Calendars { get; } = [];

        /// <summary>
        /// Gets the service-level agreements created.
        /// </summary>
        public List<SlaPolicy> Slas { get; } = [];
    }
}
