using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

// The entity type Object collides with System.Object; alias it so the
// prompt type argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Advanced-search prompt endpoint of the asset overview. The search control
    /// fetches its history and lookahead suggestions from this endpoint while the
    /// plain-text query is forwarded to the <see cref="Table"/> endpoint as the
    /// <c>q</c> parameter.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<ObjectEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
