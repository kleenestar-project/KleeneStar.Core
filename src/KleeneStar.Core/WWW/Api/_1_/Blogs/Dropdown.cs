using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Blogs
{
    /// <summary>
    /// Provides the items for the blogs dropdown in the application header: with no search
    /// term the calling identity's most recently opened blog posts (newest first); with a
    /// search term a full-text search across the blog-kind objects by summary.
    /// </summary>
    [Title("kleenestar.core:object.kind.blogs.label")]
    [Cache]
    public sealed class Dropdown : RestApiObjectKindDropdown
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dropdown()
        {
        }

        /// <summary>
        /// Gets the kind key listed by this dropdown.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Blog;
    }
}
