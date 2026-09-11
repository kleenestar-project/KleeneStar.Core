using KleeneStar.Core.WebParameter;
using System.Collections.Generic;
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
        /// Gets the renderers a post may be read and written through: prose, and nothing
        /// else.
        /// </summary>
        /// <remarks>
        /// The blog kind is the one kind that names its renderers rather than taking what
        /// the renderers offer, and it names exactly one. A post is an article on a timeline -
        /// a headline, a date, an author and a body someone wrote - and that body is what the
        /// timeline shows, what a reader opens it for, and what the editor exists to write.
        /// A structured mask has no body in that sense: it would put a post on the timeline
        /// that cannot be read as one.
        /// <para>
        /// The document kind deliberately does <b>not</b> do this. A document is a page in a
        /// tree, and a filled-in sheet standing in that tree beside the prose ones is a
        /// coherent thing - a form-rendered handbook page is still a page. Only the timeline
        /// insists on prose.
        /// </para>
        /// </remarks>
        public IEnumerable<string> Renderers => [Model.Entities.ObjectRenderer.Prose];

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
