using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// The built-in document kind: hierarchical pages that are organized as a tree
    /// via the object parent/child containment. Named "document" rather than "page"
    /// because the term page is already taken by the WebExpress page concept.
    /// </summary>
    public sealed class Document : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of documents.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Document;

        /// <summary>
        /// Gets the internationalization key of the plural display name.
        /// </summary>
        public string Label => "kleenestar.core:object.kind.documents.label";

        /// <summary>
        /// Gets the icon representing documents.
        /// </summary>
        public IIcon Icon => new IconFileLines();

        /// <summary>
        /// Gets the display order; documents lead the kind listings.
        /// </summary>
        public int Order => 1;

        /// <summary>
        /// Gets the renderer a document uses when its class names none: prose, written in
        /// the WYSIWYG editor. A class of this kind may name
        /// <see cref="Model.Entities.ObjectRenderer.Form"/> instead, and its documents then
        /// stand in the same tree but open as an input mask.
        /// </summary>
        public string DefaultRenderer => Model.Entities.ObjectRenderer.Prose;

        /// <summary>
        /// Gets the unbound route of the document overview page (the document tree).
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>();

        /// <summary>
        /// Returns the document reading view bound to the supplied object key
        /// (<c>/document/{objectkey}</c>).
        /// </summary>
        /// <param name="objectKey">The key of the document to address.</param>
        /// <returns>The bound reading-view route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Document._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns the document editing view bound to the supplied object key
        /// (<c>/document/{objectkey}/edit</c>).
        /// </summary>
        /// <param name="objectKey">The key of the document to address.</param>
        /// <returns>The bound editing-view route.</returns>
        public IUri EditUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>()?
            .BindParameters(new ObjectKeyParameter(objectKey));
    }
}
