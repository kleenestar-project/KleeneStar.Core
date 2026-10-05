using KleeneStar.Core.WebInsight;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The quickfilter bar of an insight's Kanban, Scrum, Gantt and calendar tabs: starred,
    /// assigned to me, created by me and the filters users defined. Those tabs show active
    /// objects only, so there is no archived chip.
    /// </summary>
    [Cache]
    public sealed class BoardQuickfilter : InsightQuickfilterBase
    {
        /// <inheritdoc/>
        protected override bool OffersArchived => false;
    }
}
