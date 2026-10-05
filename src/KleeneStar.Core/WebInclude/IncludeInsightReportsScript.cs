using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The script that draws the reports tab of an insight: it loads the picked report from the
    /// insight's reports endpoint and draws it with the Chart.js the framework ships. It is scoped
    /// to the insight page, the only page that carries the reports tab.
    /// </summary>
    [Asset("/assets/js/insightreports.js")]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    public sealed class IncludeInsightReportsScript : IInclude
    {
    }
}
