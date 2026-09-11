using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Dashboards
{
    /// <summary>
    /// Provides functionality to retrieve WQL data for dashboards.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<Model.Entities.Dashboard>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
