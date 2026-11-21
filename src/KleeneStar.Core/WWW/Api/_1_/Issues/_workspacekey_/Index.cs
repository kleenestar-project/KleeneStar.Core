using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_
{
    /// <summary>
    /// Declares the workspace-key segment of the issue-list REST endpoints
    /// (<c>/api/1/issues/{workspacekey}/…</c>). The named siblings
    /// (<see cref="Table"/>, <see cref="Quickfilter"/>, <see cref="Wql"/>) provide the
    /// actual data; this marker only anchors the route segment.
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
