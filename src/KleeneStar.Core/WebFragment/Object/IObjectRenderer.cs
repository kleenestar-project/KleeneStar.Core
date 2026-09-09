using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Describes a renderer - the surface an object is read and written through. Where
    /// <see cref="IObjectKind"/> decides <em>where</em> the objects of a class appear
    /// (the page tree, the timeline, the work-item list), a renderer decides <em>how</em>
    /// a single one of them is presented: as prose written in the WYSIWYG editor, or as
    /// a structured mask filed through the forms of its class.
    /// </summary>
    /// <remarks>
    /// The set of renderers is open, exactly as the set of kinds is: a plugin introduces
    /// one by implementing this interface, registering it in the
    /// <see cref="ObjectRendererCatalog"/>, and contributing the fragments that draw it -
    /// each gated on <see cref="ObjectRendererCondition"/> so it appears only where its
    /// key is the one the class named. Nothing else in the application has to know the
    /// renderer exists.
    /// <para>
    /// A renderer belongs to the kinds it can actually serve, which is what
    /// <see cref="Kinds"/> says. A renderer that serves every kind leaves the collection
    /// empty rather than listing them, so it also covers kinds that do not exist yet.
    /// </para>
    /// The persisted counterpart of a descriptor is the plain string key stored in
    /// <see cref="Model.Entities.Class.Renderer"/>.
    /// </remarks>
    public interface IObjectRenderer
    {
        /// <summary>
        /// Gets the renderer key persisted in <see cref="Model.Entities.Class.Renderer"/>.
        /// Keys are lower-case and compared case-insensitively; the core keys are defined
        /// in <see cref="Model.Entities.ObjectRenderer"/>.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the renderer's display name (e.g. "Form"),
        /// used for the renderer picker on the class dialogs.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the internationalization key of a one-line description of what the
        /// renderer does to the reading and the editing view.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon representing the renderer.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the display order of the renderer within renderer listings. Lower values
        /// are listed first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the keys of the object kinds this renderer serves. An <b>empty</b>
        /// collection means every kind, including kinds registered later - it is the
        /// declaration of a renderer that needs nothing of a kind beyond it having a
        /// class.
        /// </summary>
        IEnumerable<string> Kinds => [];
    }
}
