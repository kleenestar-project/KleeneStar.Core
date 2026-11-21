using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The list of an insight's objects tab: the objects its query selects as a master-detail
    /// list, sharing the search, the quickfilters and the paging with the table beside it.
    /// </summary>
    [Cache]
    public sealed class List : RestApiObjectKindList
    {
        /// <summary>
        /// Gets the kind the base falls back to; the scope below replaces the kind narrowing.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the key the user-defined quickfilters of insights are stored under.
        /// </summary>
        protected override string ViewKey => InsightScope.QuickfilterView;

        /// <summary>
        /// Narrows a query to the objects of the insight the route names, or to nothing for a
        /// caller who may not read it.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Object> Scope(IQuery<Model.Entities.Object> query, IRequest request)
        {
            var insight = InsightScope.ResolveReadable(request);

            return insight is null
                ? query.Where(x => false)
                : InsightScope.Apply(insight, query);
        }
    }
}
