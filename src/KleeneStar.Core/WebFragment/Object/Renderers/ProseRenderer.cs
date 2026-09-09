using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Renderers
{
    /// <summary>
    /// The built-in prose renderer: the object is written in the WYSIWYG editor and read
    /// as a page of text. It is what a document and a blog post have always been, and the
    /// default of both kinds - a class that names no renderer renders through this one.
    /// </summary>
    /// <remarks>
    /// The surfaces it draws are <see cref="ObjectProseReadFragment"/> on the reading views
    /// and <see cref="ObjectProseEditorFragmentBase"/>'s two subclasses on the reading and
    /// the edit route; each is gated on <see cref="ProseRendererCondition"/>. It serves the
    /// document and blog kinds only: an issue has no body a WYSIWYG editor could be the
    /// whole of.
    /// </remarks>
    public sealed class ProseRenderer : IObjectRenderer
    {
        /// <summary>
        /// Gets the persisted renderer key.
        /// </summary>
        public string Key => Model.Entities.ObjectRenderer.Prose;

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Label => "kleenestar.core:object.renderer.prose.label";

        /// <summary>
        /// Gets the internationalization key of the one-line description.
        /// </summary>
        public string Description => "kleenestar.core:object.renderer.prose.description";

        /// <summary>
        /// Gets the icon representing the prose renderer.
        /// </summary>
        public IIcon Icon => new IconAlignLeft();

        /// <summary>
        /// Gets the display order; prose leads the renderer listings because it is the
        /// renderer the kinds that offer a choice default to.
        /// </summary>
        public int Order => 1;

        /// <summary>
        /// Gets the kinds prose serves: the two that have a body to write.
        /// </summary>
        public IEnumerable<string> Kinds =>
        [
            Model.Entities.ObjectKind.Document,
            Model.Entities.ObjectKind.Blog
        ];
    }
}
