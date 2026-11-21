using KleeneStar.Core.WebFragment.Object;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The object kinds (subtypes) known to the installation - the semantic lookup behind the
    /// persisted <see cref="Model.Entities.Object.Kind"/> key. It deliberately does not gate
    /// persistence: unknown keys survive in the data layer, so objects of an add-on kind outlive
    /// the add-on, but every surface resolves a key to its presentation (label, icon, routes) here.
    /// </summary>
    public interface IObjectKindManager : IComponentManager
    {
        /// <summary>
        /// Gets a number that changes whenever the set of kinds does, watched by the cached
        /// dialog controls together with <see cref="IObjectRendererManager.Version"/>: the
        /// renderers a kind offers are the answer of the two together.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Gets the kinds, ordered by <see cref="IObjectKind.Order"/> and then by key.
        /// </summary>
        IEnumerable<IObjectKind> Kinds { get; }

        /// <summary>
        /// Registers a kind that is no type of a plugin. Its key is normalized via
        /// <see cref="Model.Entities.ObjectKind.Normalize"/>; a key that is present is taken
        /// over, so an add-on may override the presentation of a built-in kind.
        /// </summary>
        /// <param name="kind">The kind. Must not be null.</param>
        void Register(IObjectKind kind);

        /// <summary>
        /// Resolves a kind key. The key is normalized first, so null or whitespace resolves to
        /// the default kind.
        /// </summary>
        /// <param name="key">The kind key. May be null.</param>
        /// <returns>The kind, or <see langword="null"/> when none is known under the key (the key
        /// of an uninstalled add-on, for instance).</returns>
        IObjectKind GetKind(string key);

        /// <summary>
        /// Resolves the detail (reading) view route of an object, addressed by its kind - the
        /// single dispatch point every object link uses.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The bound route, or <see langword="null"/> for no object.</returns>
        IUri ResolveDetailUri(Model.Entities.Object @object);

        /// <summary>
        /// Resolves the detail view route for a kind key and an object key. An unknown kind
        /// falls back to the issue view so the link still leads somewhere meaningful.
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="objectKey">The object key. May be null.</param>
        /// <returns>The bound route.</returns>
        IUri ResolveDetailUri(string kind, string objectKey);

        /// <summary>
        /// Resolves the reduced reading view of an object - the view a master-detail pane
        /// fetches for the selected row.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The bound route, or <see langword="null"/> for no object.</returns>
        IUri ResolvePreviewUri(Model.Entities.Object @object);

        /// <summary>
        /// Resolves the reduced reading view for a kind key and an object key, falling back to
        /// the issue kind like <see cref="ResolveDetailUri(string, string)"/>.
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="objectKey">The object key. May be null.</param>
        /// <returns>The bound route.</returns>
        IUri ResolvePreviewUri(string kind, string objectKey);

        /// <summary>
        /// Resolves the detail route of an object with its trailing object-key segment frozen
        /// into a constant, so a control that re-binds its link against the current request (the
        /// sidebar link on a detail page) cannot rewrite it to point at the current object.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The frozen route, or <see langword="null"/> for no object.</returns>
        IUri ResolveDetailUriFrozen(Model.Entities.Object @object);

        /// <summary>
        /// Resolves the dedicated edit view route of an object, or <see langword="null"/> for a
        /// kind that edits inline or in a dialog (the issue kind).
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The bound route, or <see langword="null"/>.</returns>
        IUri ResolveEditUri(Model.Entities.Object @object);

        /// <summary>
        /// Resolves the dedicated edit view route for a kind key and an object key.
        /// </summary>
        /// <param name="kind">The kind key. May be null.</param>
        /// <param name="objectKey">The object key. May be null.</param>
        /// <returns>The bound route, or <see langword="null"/>.</returns>
        IUri ResolveEditUri(string kind, string objectKey);
    }
}
