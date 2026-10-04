using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// Serves as the routing parent for insight-specific REST API endpoints,
    /// binding the insight id path segment.
    /// </summary>
    [Cache]
    [InsightIdSegment]
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
