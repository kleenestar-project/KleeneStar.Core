using KleeneStar.Core.WebFragment.Object;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The renderers known to the installation - the semantic lookup behind the persisted
    /// <see cref="Model.Entities.Class.Renderer"/> key, answering the one question every
    /// rendering surface asks: <em>is this object mine to draw?</em> Like the kinds it does not
    /// gate persistence: an unknown key survives in the data, and the class reads as the default
    /// of its kind while the add-on that gave it the renderer is gone.
    /// </summary>
    public interface IObjectRendererManager : IComponentManager
    {
        /// <summary>
        /// Gets a number that changes whenever the set of renderers does. The renderer picker
        /// on the class dialogs is a control on a cached fragment and projects the renderers
        /// again only when this number (or the kinds') has moved.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Gets the renderers, ordered by <see cref="IObjectRenderer.Order"/> and then by key.
        /// </summary>
        IEnumerable<IObjectRenderer> Renderers { get; }

        /// <summary>
        /// Registers a renderer that is no type of a plugin. Its key is normalized via
        /// <see cref="Model.Entities.ObjectRenderer.Normalize"/>; a key that is present is taken
        /// over. The key <see cref="ObjectRendererManager.Automatic"/> is reserved.
        /// </summary>
        /// <param name="renderer">The renderer. Must not be null and must carry a key.</param>
        void Register(IObjectRenderer renderer);

        /// <summary>
        /// Resolves a renderer key.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The renderer, or <see langword="null"/> when the key is unset or nobody
        /// provides it.</returns>
        IObjectRenderer GetRenderer(string key);

        /// <summary>
        /// Gets the renderers a kind offers: the ones that serve it (by declaring it, or no kind
        /// at all), narrowed to the ones the kind accepts (<see cref="IObjectKind.Renderers"/>,
        /// empty meaning all).
        /// </summary>
        /// <param name="kind">The kind key. May be null, which resolves to the default kind.</param>
        /// <returns>The renderers, in listing order.</returns>
        IEnumerable<IObjectRenderer> GetRenderers(string kind);

        /// <summary>
        /// Gets the keys of the kinds that offer a renderer - the inverse of
        /// <see cref="GetRenderers(string)"/>, which the picker needs to narrow itself to the
        /// kind chosen beside it.
        /// </summary>
        /// <param name="renderer">The renderer key. May be null, which answers nothing.</param>
        /// <returns>The kind keys, in the listing order of the kinds.</returns>
        IEnumerable<string> GetKinds(string renderer);

        /// <summary>
        /// Determines whether a renderer key is one the kind offers. An unset key is always
        /// acceptable - it is the class saying "follow the kind".
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="renderer">The renderer key. May be null.</param>
        /// <returns>True when the key is unset or names a renderer the kind offers.</returns>
        bool IsOffered(string kind, string renderer);

        /// <summary>
        /// Resolves the renderer key in effect for a class: the one it names, or - when it names
        /// none, one nobody provides, or one its kind does not offer - the default of its kind.
        /// </summary>
        /// <param name="class">The class. May be null.</param>
        /// <returns>The effective key. Never null.</returns>
        string ResolveKey(Model.Entities.Class @class);

        /// <summary>
        /// Resolves the renderer key in effect for a kind and a configured renderer.
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="renderer">The configured renderer key. May be null.</param>
        /// <returns>The effective key. Never null.</returns>
        string ResolveKey(string kind, string renderer);

        /// <summary>
        /// Resolves the renderer in effect for a class.
        /// </summary>
        /// <param name="class">The class. May be null.</param>
        /// <returns>The renderer, or <see langword="null"/> when the effective key is not provided either.</returns>
        IObjectRenderer Resolve(Model.Entities.Class @class);

        /// <summary>
        /// Determines whether a class is rendered by the renderer with a key - the question
        /// every renderer-specific fragment asks about the object it was about to draw.
        /// </summary>
        /// <param name="class">The class. May be null, which answers false.</param>
        /// <param name="renderer">The renderer key.</param>
        /// <returns>True when the class renders through that renderer.</returns>
        bool IsRenderedAs(Model.Entities.Class @class, string renderer);

        /// <summary>
        /// Determines whether a class reads and writes its objects through fields and forms
        /// (<see cref="IObjectRenderer.Structured"/> of the effective renderer).
        /// </summary>
        /// <param name="class">The class. May be null, which answers false.</param>
        /// <returns>True when the effective renderer is a mask.</returns>
        bool IsStructured(Model.Entities.Class @class);
    }
}
