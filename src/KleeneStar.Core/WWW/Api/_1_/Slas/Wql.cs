using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Slas
{
    /// <summary>
    /// Provides WQL prompt suggestions for the SLA-policy advanced search.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<SlaPolicy>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
