using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Blogs._workspacekey_
{
    /// <summary>
    /// Provides a index for the posts of a workspace. Declaring the workspace-key segment here is
    /// what turns the folder into a route variable, so the sibling endpoints receive the addressed
    /// workspace instead of a literal path segment.
    /// </summary>
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }
    }
}
