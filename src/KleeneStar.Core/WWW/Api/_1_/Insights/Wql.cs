using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Provides functionality to retrieve WQL data for insights.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<Model.Entities.Insight>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
