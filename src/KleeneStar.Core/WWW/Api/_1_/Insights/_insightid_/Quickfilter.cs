using KleeneStar.Core.WebInsight;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The quickfilter bar of an insight's objects tab, shared by its table and its list:
    /// starred, assigned to me, created by me, archived, and the filters users defined.
    /// </summary>
    [Cache]
    public sealed class Quickfilter : InsightQuickfilterBase
    {
        /// <inheritdoc/>
        protected override bool OffersArchived => true;
    }
}
