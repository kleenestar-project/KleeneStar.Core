using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

// The entity type Object collides with System.Object; alias it so the signatures read
// naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The table of an insight's objects tab: the objects its query selects, with the search,
    /// the quickfilters, the column chooser and the paging of the workspace overview's table.
    /// </summary>
    /// <remarks>
    /// The columns are the fields of the classes the objects come from, whichever workspaces
    /// those are in, and the layout a user picks is stored per insight and per tab.
    /// </remarks>
    [Cache]
    public sealed class Table : RestApiObjectKindTable
    {
        /// <summary>
        /// Gets the kind the base falls back to; an insight lists objects of every kind, and each
        /// row links the detail page of its own.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the key the user-defined quickfilters of insights are stored under.
        /// </summary>
        protected override string ViewKey => InsightScope.QuickfilterView;

        /// <summary>
        /// Returns the objects of the insight the route names, or none for a caller who may not
        /// read it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The objects.</returns>
        protected override IReadOnlyList<ObjectEntity> RetrieveScope(IRequest request)
        {
            return InsightScope.GetObjects(InsightScope.ResolveReadable(request));
        }

        /// <summary>
        /// Offers the fields of the classes the insight's objects come from as columns.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The column catalog.</returns>
        private protected override ObjectTableColumnCatalog BuildCatalog(IRequest request)
        {
            var objects = InsightScope.GetObjects(InsightScope.ResolveReadable(request));

            return ObjectTableColumnCatalog.Build(objects.Select(x => x.ClassId), request);
        }

        /// <summary>
        /// Keeps the column layouts of insights apart by the insight.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The insight id of the route.</returns>
        protected override string LayoutScope(IRequest request)
        {
            return request?.GetParameter<WebParameter.InsightIdParameter>()?.Value;
        }
    }
}
