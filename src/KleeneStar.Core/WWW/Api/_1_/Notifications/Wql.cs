using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Notifications
{
    /// <summary>
    /// Provides the search prompt of the notification center.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<UserNotification>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
