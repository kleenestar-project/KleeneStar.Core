using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One field a <see cref="WorkspaceTemplateClass"/> is created with.
    /// </summary>
    /// <remarks>
    /// Like the class it belongs to it is a description, not the <see cref="Field"/> entity: it
    /// exists once in the template and many times over in the workspaces created from it. The
    /// forms of the class are derived from the fields - every field lands on the tab it names,
    /// in the order the template declares them - so a template states where a field belongs
    /// rather than building three forms by hand that would have to agree with each other.
    /// </remarks>
    public sealed class WorkspaceTemplateField
    {
        /// <summary>
        /// The tab a field lands on when it names none - an internationalization key, resolved
        /// into the tab's name when the form is written.
        /// </summary>
        public const string GeneralTab = "kleenestar.core:workspace.template.tab.general";

        /// <summary>
        /// The tab of the fields that are worth keeping but not worth reading first.
        /// </summary>
        public const string DetailsTab = "kleenestar.core:workspace.template.tab.details";

        /// <summary>
        /// Gets the name of the field - an internationalization key or plain text, resolved once
        /// when the workspace is created and stored as data from then on. A field named after a
        /// system attribute of the object (<c>Description</c>) has to keep that name: it is what
        /// binds the answer to the object rather than to a value row.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the field records - an internationalization key or plain text, resolved
        /// once when the workspace is created.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the path of the icon the field is created with.
        /// </summary>
        public string Icon { get; init; }

        /// <summary>
        /// Gets the type of the field. A <see cref="FieldType.Workflow"/> field is bound to the
        /// workflow of its class, so a class declares at most one.
        /// </summary>
        public FieldType Type { get; init; } = FieldType.Text;

        /// <summary>
        /// Gets the choices of a selecting field (selection, choice, tile, radio, …).
        /// </summary>
        public IReadOnlyList<string> Options { get; init; } = [];

        /// <summary>
        /// Gets whether the field has to be answered.
        /// </summary>
        public bool Required { get; init; }

        /// <summary>
        /// Gets the tab of the standard forms the field is placed on - an internationalization
        /// key or plain text.
        /// </summary>
        public string Tab { get; init; } = GeneralTab;

        /// <summary>
        /// Gets whether the field is asked when the object is created. A field that only the
        /// team fills in later - a root cause, a resolution code - is left out of the create
        /// form and still stands on the edit and view forms.
        /// </summary>
        public bool OnCreate { get; init; } = true;

        /// <summary>
        /// Gets whether the field is asked on the self-service form of a portal-visible class,
        /// the form customers file a request through.
        /// </summary>
        public bool Portal { get; init; }
    }
}
