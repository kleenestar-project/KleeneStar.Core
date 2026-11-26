using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes <c>Assets/js/widgets/kleenestar.js</c>, the KleeneStar widget types of the
    /// editable dashboards - the insight view and the dashboard tabs of issues and assets.
    /// </summary>
    /// <remarks>
    /// Every page, because the widgets register with the client registry before the controls of
    /// the page are built, and a board finds only the types registered by then. The widgets
    /// translate their titles and settings labels when they register, so the translations come
    /// first and are declared here as well: the order of two includes of one plugin is not
    /// defined, and registering the translations a second time merges, it does not conflict.
    /// </remarks>
    [Asset("/assets/js/i18n/en.js")]
    [Asset("/assets/js/i18n/de.js")]
    [Asset("/assets/js/widgets/kleenestar.js")]
    public sealed class IncludeDashboardWidgetsScript : IInclude
    {
    }
}
