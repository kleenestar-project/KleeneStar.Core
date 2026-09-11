using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Priorities
{
    /// <summary>
    /// Provides functionality to retrieve WQL data.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<Model.Entities.Priority>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
