using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.TemplateHierarchies._workspacekey_
{
    /// <summary>
    /// The index of the hierarchies of a workspace. Declaring the workspace-key segment here is
    /// what turns the folder into a route variable for the sibling endpoints.
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
