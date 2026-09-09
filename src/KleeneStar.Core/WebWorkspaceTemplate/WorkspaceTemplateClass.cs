using KleeneStar.Model.Entities;

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
    }
}
