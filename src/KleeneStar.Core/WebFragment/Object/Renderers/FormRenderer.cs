using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Renderers
{
    /// <summary>
    /// The built-in form renderer: the object is written through the structured input mask
    /// its class's <see cref="Model.Entities.FormType.Edit"/> form describes, and read as
    /// an unchangeable view of its <see cref="Model.Entities.FormType.View"/> form.
    /// </summary>
    /// <remarks>
    /// It declares <b>no</b> kinds, which means every kind: a mask needs nothing of a kind
    /// beyond the class having fields and forms, and every class has both. It is therefore
    /// also what an add-on kind gets for free, and it is the default of the issue and asset
    /// kinds - whose detail and edit views have always been exactly this.
    /// <para>
    /// On the document and blog kinds it is the alternative to
    /// <see cref="ProseRenderer"/>: the object still stands in the page tree or the
    /// timeline beside the prose ones - the kind is unchanged and so is everything the kind
    /// decides - but it opens as a form. The surfaces are
    /// <see cref="ObjectFormReadFragment"/> and <see cref="ObjectFormEditFragment"/>, each
    /// gated on <see cref="FormRendererCondition"/>.
    /// </para>
    /// </remarks>
    public sealed class FormRenderer : IObjectRenderer
    {
        /// <summary>
        /// Gets the persisted renderer key.
        /// </summary>
        public string Key => Model.Entities.ObjectRenderer.Form;

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Label => "kleenestar.core:object.renderer.form.label";

        /// <summary>
        /// Gets the internationalization key of the one-line description.
        /// </summary>
        public string Description => "kleenestar.core:object.renderer.form.description";

        /// <summary>
        /// Gets the icon representing the form renderer.
        /// </summary>
        public IIcon Icon => new IconRectangleList();

        /// <summary>
        /// Gets the display order; the form follows the prose.
        /// </summary>
        public int Order => 2;

        /// <summary>
        /// Gets the kinds the form serves - none in particular, and therefore all of them,
        /// including kinds registered after it.
        /// </summary>
        public IEnumerable<string> Kinds => [];
    }
}
