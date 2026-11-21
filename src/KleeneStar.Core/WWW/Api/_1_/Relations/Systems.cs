using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Relations
{
    /// <summary>
    /// REST endpoint the add dialog of the <c>ControlDataRelationView</c> reads its sidebar
    /// from: the registered link systems and the relations each of them offers. The URL is
    /// <c>/api/1/relations/systems</c>.
    /// </summary>
    /// <remarks>
    /// The answer is derived entirely from the framework registry, which
    /// <see cref="WebManager.IObjectRelationTypeManager.Publish"/> fills from this
    /// installation's relation catalog at startup and after every change to it. The endpoint
    /// therefore needs no code of its own: adding a relation in the class administration makes
    /// it appear in this dialog without anything here being touched.
    /// </remarks>
    [Title("kleenestar.core:relation.systems.api.title")]
    [Cache]
    public sealed class Systems : RestApiRelationSystem
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Systems()
        {
        }
    }
}
