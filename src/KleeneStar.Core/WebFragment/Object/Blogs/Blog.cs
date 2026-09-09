using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Blogs
{
    /// <summary>
    /// The built-in blog kind: chronological posts that are presented as a
    /// timeline grouped by year and month, newest first.
    /// </summary>
    public sealed class Blog : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of blog posts.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Blog;

        /// <summary>
        /// Gets the internationalization key of the plural display name.
        /// </summary>
        public string Label => "kleenestar.core:object.kind.blogs.label";

        /// <summary>
        /// Gets the icon representing blog posts.
        /// </summary>
        public IIcon Icon => new IconBlog();

        /// <summary>
        /// Gets the display order; blogs follow the documents.
        /// </summary>
        public int Order => 2;

        /// <summary>
        /// Gets the renderer a post uses when its class names none: prose, written in the
        /// WYSIWYG editor.
        /// </summary>
        public string DefaultRenderer => Model.Entities.ObjectRenderer.Prose;

        /// <summary>
        /// Gets the unbound route of the blog overview page (the blog timeline).
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>();

        /// <summary>
        /// Returns the blog reading view bound to the supplied object key
        /// (<c>/blog/{objectkey}</c>).
        /// </summary>
        /// <param name="objectKey">The key of the post to address.</param>
        /// <returns>The bound reading-view route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns the blog editing view bound to the supplied object key
        /// (<c>/blog/{objectkey}/edit</c>).
        /// </summary>
        /// <param name="objectKey">The key of the post to address.</param>
        /// <returns>The bound editing-view route.</returns>
        public IUri EditUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>()?
            .BindParameters(new ObjectKeyParameter(objectKey));
    }
}
