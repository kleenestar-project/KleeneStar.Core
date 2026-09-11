using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Describes an object kind (subtype) such as document, blog, or issue. A kind
    /// partitions the objects of a workspace and decides which overview view presents
    /// them: documents form a hierarchical page tree, blog posts a chronological
    /// timeline, and issues a filterable work-item list.
    /// </summary>
    /// <remarks>
    /// The set of kinds is open: add-ons introduce a new kind by implementing this
    /// interface, registering the descriptor in the <see cref="ObjectKindCatalog"/>
    /// (typically from their plugin initialization), contributing an overview page,
    /// and deriving a sidebar link from <see cref="ObjectKindSidebarLinkFragment"/>.
    /// The persisted counterpart of a descriptor is the plain string key stored in
    /// <see cref="Model.Entities.Object.Kind"/>.
    /// </remarks>
    public interface IObjectKind
    {
        /// <summary>
        /// Gets the kind key persisted in <see cref="Model.Entities.Object.Kind"/>.
        /// Keys are lower-case and compared case-insensitively; the core keys are
        /// defined in <see cref="Model.Entities.ObjectKind"/>.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the kind's plural display name
        /// (e.g. "Documents"), used for sidebar links and headlines.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the icon representing the kind.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the display order of the kind within kind listings such as the
        /// objects sidebar. Lower values are listed first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the renderer key a class of this kind uses when it names none of its own -
        /// the answer to what this kind has always looked like.
        /// </summary>
        /// <remarks>
        /// A kind and a renderer are two different questions: the kind decides where the
        /// objects of a class appear, the renderer how one of them is read and written.
        /// The default is the structured mask, because that needs nothing of a kind beyond
        /// its classes having fields - the two kinds that have a body to write override it
        /// with <see cref="Model.Entities.ObjectRenderer.Prose"/>. The default
        /// implementation keeps add-on kinds compiling and gives them a working pair of
        /// views without any work of their own.
        /// </remarks>
        string DefaultRenderer => Model.Entities.ObjectRenderer.Form;

        /// <summary>
        /// Gets the keys of the renderers this kind accepts. An <b>empty</b> collection means
        /// every renderer that serves the kind, including renderers registered later - it is
        /// the declaration of a kind that has no opinion beyond what the renderers themselves
        /// say.
        /// </summary>
        /// <remarks>
        /// This is the counterpart of <see cref="IObjectRenderer.Kinds"/>, and a renderer is
        /// offered only where <em>both</em> sides say so. The two exist because the two
        /// statements are genuinely different ones: a renderer knows which kinds it is capable
        /// of drawing, while a kind knows which of them it is willing to be read through -
        /// prose declines the issue kind because it has no body to write, and the blog kind
        /// declines the mask because a post is prose by definition. Neither could be expressed
        /// from the other side without the universal renderers having to enumerate every kind
        /// that ever exists, which is exactly what the empty collection is there to avoid.
        /// <para>
        /// A kind that names renderers is a closed list, so a renderer an add-on contributes
        /// later is <em>not</em> offered on it until the kind names that too - which is the
        /// point of naming any: the restriction has to survive the arrival of surfaces its
        /// author never saw.
        /// </para>
        /// </remarks>
        IEnumerable<string> Renderers => [];

        /// <summary>
        /// Gets the unbound route of the kind's overview page. The route carries the
        /// workspace-key segment, so callers bind the current request (or an explicit
        /// workspace-key parameter) before navigating.
        /// </summary>
        IUri OverviewUri { get; }

        /// <summary>
        /// Returns the route of the kind's detail (reading) view bound to the supplied
        /// object key, e.g. <c>/issue/{objectkey}</c> or <c>/document/{objectkey}</c>.
        /// Every kind has a detail view, so this is expected to be non-null for a
        /// registered kind.
        /// </summary>
        /// <param name="objectKey">The key of the object to address. May be null.</param>
        /// <returns>The bound detail route, or <see langword="null"/> when the kind has
        /// no dedicated detail view.</returns>
        IUri DetailUri(string objectKey);

        /// <summary>
        /// Returns the route of the reduced reading view bound to the supplied object key -
        /// the view a master-detail pane shows for a selected row, as opposed to the full
        /// reading view <see cref="DetailUri(string)"/> names.
        /// </summary>
        /// <remarks>
        /// Every kind shares one reduced view by default, because what a detail pane shows is
        /// the object itself rather than the arrangement its kind reads best in: the shared
        /// view is addressed by object key alone and composes from the object's class. A kind
        /// that genuinely needs its own reduced view overrides this member; the default keeps
        /// add-on kinds compiling and gives them a working pane without any work of their own.
        /// </remarks>
        /// <param name="objectKey">The key of the object to address. May be null.</param>
        /// <returns>The bound reduced-view route.</returns>
        IUri PreviewUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>()?
            .BindParameters(new WebParameter.ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns the route of the kind's dedicated editing view bound to the supplied
        /// object key, e.g. <c>/document/{objectkey}/edit</c>. Returns
        /// <see langword="null"/> for kinds that edit inline or through a modal rather
        /// than on a dedicated page (the issue kind edits via a modal, so it has no edit
        /// route).
        /// </summary>
        /// <param name="objectKey">The key of the object to address. May be null.</param>
        /// <returns>The bound edit route, or <see langword="null"/> when the kind has no
        /// dedicated edit view.</returns>
        IUri EditUri(string objectKey);
    }
}
