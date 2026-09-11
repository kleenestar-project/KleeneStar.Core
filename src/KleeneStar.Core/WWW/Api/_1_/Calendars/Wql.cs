using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Calendars
{
    using Calendar = KleeneStar.Model.Entities.Calendar;

    /// <summary>
    /// Provides WQL prompt suggestions for the calendar advanced search.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<Calendar>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
