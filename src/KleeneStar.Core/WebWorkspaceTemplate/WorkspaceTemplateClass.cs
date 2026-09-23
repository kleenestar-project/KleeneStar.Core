using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One class a <see cref="IWorkspaceTemplate"/> creates in the workspace it shapes.
    /// </summary>
    /// <remarks>
    /// It is deliberately not the <see cref="Class"/> entity. A template describes a class that
    /// does not exist yet and will exist many times over - once per workspace created from it -
    /// so it can carry no id, no workspace and no timestamps, and an entity handed around with
    /// those three left empty would invite exactly the bug of saving it. What it carries instead
    /// is the part somebody actually decided: the name, what it is for, which kind of object it
    /// holds, and whether customers may see it.
    /// </remarks>
    public sealed class WorkspaceTemplateClass
    {
        /// <summary>
        /// Gets the name of the class, e.g. <c>Incident</c>. It is not translated: a class name
        /// is data an administrator renames, not a caption of the product.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the internationalization key of the sentence saying what the class holds.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the path of the icon the class is created with.
        /// </summary>
        public string Icon { get; init; }

        /// <summary>
        /// Gets the kind of object the class holds - an issue, a document, a post, an asset.
        /// Defaults to <see cref="ObjectKind.Issue"/>.
        /// </summary>
        public string Kind { get; init; } = ObjectKind.Issue;

        /// <summary>
        /// Gets the renderer the objects of the class are read and written through - prose in
        /// the WYSIWYG editor, or the structured input mask of the class's forms. Left
        /// <see langword="null"/> the class follows the default of its <see cref="Kind"/>,
        /// which is what a template that does not care should leave it at.
        /// </summary>
        /// <remarks>
        /// It is separate from <see cref="Kind"/> because the two decide different things: the
        /// kind decides in which overview the objects appear, the renderer how one of them
        /// opens. A template may therefore ship a document class whose pages are filled in
        /// rather than written, standing in the same page tree as the prose ones.
        /// </remarks>
        public string Renderer { get; init; }

        /// <summary>
        /// Gets whether objects of this class are offered in the customer portal.
        /// </summary>
        public bool PortalVisible { get; init; }

        /// <summary>
        /// Gets whether the class may not be specialized further.
        /// </summary>
        public bool Sealed { get; init; }

        /// <summary>
        /// Gets who may see the class.
        /// </summary>
        public AccessModifier AccessModifier { get; init; } = AccessModifier.Public;

        /// <summary>
        /// Gets the fields the class is created with, in the order its forms show them. The
        /// create, edit and view forms are derived from them.
        /// </summary>
        /// <remarks>
        /// Everything below describes the <em>structure</em> of the class, and all of it is
        /// the template's to decide - the core applies what is declared and adds nothing of
        /// its own. What is left empty is not created, so a prose class that declares no
        /// fields gets no forms either, which is right: prose has no fields.
        /// </remarks>
        public IReadOnlyList<WorkspaceTemplateField> Fields { get; init; } = [];

        /// <summary>
        /// Gets the priority scale of the class, most pressing first.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplatePriority> Priorities { get; init; } = [];

        /// <summary>
        /// Gets the lifecycle of the class, or null for a class without one.
        /// </summary>
        public WorkspaceTemplateWorkflow Workflow { get; init; }

        /// <summary>
        /// Gets the calendars the service-level clocks of the class run in.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateCalendar> Calendars { get; init; } = [];

        /// <summary>
        /// Gets the service-level agreements of the class.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateSla> Slas { get; init; } = [];
    }
}
