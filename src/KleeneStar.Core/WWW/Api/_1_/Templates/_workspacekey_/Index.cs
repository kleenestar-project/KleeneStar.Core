using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_
{
    /// <summary>
    /// Provides a index for template items. Declaring the workspace-key segment here is what
    /// turns the folder into a route variable, so the sibling endpoints (table, quickfilter)
    /// receive the addressed workspace instead of a literal path segment.
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
