using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The calendar of an insight: its active objects on a month, week or agenda grid, placed by
    /// the date fields of their classes. Moving an entry writes the dates back, as on a
    /// workspace's calendar.
    /// </summary>
    [Cache]
    public sealed class Calendar : RestApiObjectKindSchedule
    {
        /// <summary>
        /// Gets the kind the base falls back to; the scope below replaces the kind narrowing.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Returns the query over the insight's objects, or none for a caller who may not read it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The query, or <see langword="null"/>.</returns>
        protected override IQuery<Model.Entities.Object> ResolveScope(IRequest request)
        {
            var insight = InsightScope.ResolveReadable(request);

            return insight is null ? null : InsightScope.Query(insight);
        }

        /// <summary>
        /// Accepts a moved entry only for an object the insight selects.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the object is on the calendar.</returns>
        protected override bool InScope(Model.Entities.Object entity, IRequest request)
        {
            return InsightScope.Contains(InsightScope.ResolveReadable(request), entity);
        }

        /// <summary>
        /// Applies the search term and the quickfilter chips of the calendar header.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request.</param>
        /// <returns>The filtered objects.</returns>
        protected override IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            return ObjectKindBoardFilter.Apply(objects, request, InsightScope.QuickfilterView);
        }
    }
}
