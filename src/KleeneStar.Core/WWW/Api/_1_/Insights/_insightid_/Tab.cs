using KleeneStar.Core.WebInsight;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// REST API endpoint that returns and arranges the tabs (<see cref="InsightView"/>) of an
    /// insight - the counterpart of the tab endpoint of a workspace overview.
    /// </summary>
    /// <remarks>
    /// Each tab is bound to the tab template its view type names in the
    /// <see cref="InsightViewTypeCatalog"/>; a tab whose type nobody registers any more is left
    /// out (and kept), and a template the catalog does not know cannot be added. Reading needs the
    /// right to read the insight's content, arranging - adding, removing, reordering - the right
    /// to change it.
    /// </remarks>
    [Title("kleenestar.core:insight.tab.header")]
    [Cache]
    public sealed class Tab : RestApiTab<Model.Entities.Insight>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tab()
        {
        }

        /// <summary>
        /// Creates the query context of the endpoint.
        /// </summary>
        /// <returns>A database context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the tabs of the insight the route names, in display order.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The tabs.</returns>
        protected override IEnumerable<RestApiTabView> RetrieveViews(IQueryContext context, IRequest request)
        {
            var insight = InsightScope.ResolveReadable(request);

            if (insight is null)
            {
                return [];
            }

            return CoreHub.InsightManager.GetViews(insight.Id)
                .Where(x => x.State == ObjectViewState.Active)
                .Select(x => (View: x, Type: InsightViewTypeCatalog.Get(x.ViewType)))
                .Where(x => x.Type is not null)
                .Select(x => ToTab(x.View, x.Type, request))
                .ToList();
        }

        /// <summary>
        /// Adds a tab of the type whose template the client picked.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <param name="templateId">The id of the picked template.</param>
        /// <returns>The new tab, or <see langword="null"/> when nothing was added.</returns>
        protected override IRestApiTabView CreateView(IQueryContext context, IRequest request, string templateId)
        {
            var insight = InsightScope.Resolve(request);

            if (!InsightScope.MayArrange(insight, request))
            {
                return null;
            }

            var type = InsightViewTypeCatalog.FromTemplateId(templateId)
                ?? InsightViewTypeCatalog.Get(InsightViewTypeCatalog.Default);

            if (type is null)
            {
                return null;
            }

            var view = new InsightView
            {
                InsightId = insight.Id,
                ViewType = InsightViewTypeCatalog.Normalize(type.Key),
                Name = I18N.Translate(request, type.Label),
                State = ObjectViewState.Active
            };

            CoreHub.InsightManager.AddView(view);

            return ToTab(view, type, request);
        }

        /// <summary>
        /// Removes a tab of the insight the route names.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <returns><see langword="true"/> when the tab was removed.</returns>
        protected override bool RemoveView(string viewId)
        {
            if (!Guid.TryParse(viewId, out var id))
            {
                return false;
            }

            var view = CoreHub.InsightManager.GetView(id);
            var request = WebExpress.WebCore.WebEx.CurrentRequest;
            var insight = view is null ? null : CoreHub.InsightManager.GetInsight(view.InsightId);

            // the route names the insight; a tab of another insight is not reachable through it
            if (insight is null
                || !string.Equals(insight.Id.ToString(), request?.GetParameter<WebParameter.InsightIdParameter>()?.Value, StringComparison.OrdinalIgnoreCase)
                || !InsightScope.MayArrange(insight, request))
            {
                return false;
            }

            return CoreHub.InsightManager.RemoveView(id);
        }

        /// <summary>
        /// Persists the order the tabs were dragged into.
        /// </summary>
        /// <param name="order">The tab ids in their new order.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        protected override bool ReorderViews(IReadOnlyList<string> order, IQueryContext context, IRequest request)
        {
            var insight = InsightScope.Resolve(request);

            if (order is null || !InsightScope.MayArrange(insight, request))
            {
                return false;
            }

            var ids = order
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToList();

            return CoreHub.InsightManager.ReorderViews(insight.Id, ids);
        }

        /// <summary>
        /// Projects a tab onto the shape the tab control reads.
        /// </summary>
        /// <param name="view">The tab.</param>
        /// <param name="type">Its view type.</param>
        /// <param name="request">The request.</param>
        /// <returns>The tab.</returns>
        private static RestApiTabView ToTab(InsightView view, IInsightViewType type, IRequest request)
        {
            return new RestApiTabView
            {
                Id = view.Id.ToString(),
                Name = view.Name,
                Title = view.Name,
                Icon = (type.Icon as WebExpress.WebUI.WebIcon.Icon)?.Class,
                TemplateId = InsightViewTypeCatalog.TemplateId(type),
                Binding = BuildBinding(view, request)
            };
        }

        /// <summary>
        /// Builds the binding payload of a tab: the values its template writes into its controls
        /// when the client instantiates it for this tab.
        /// </summary>
        /// <remarks>
        /// A template renders once and is cloned per tab, so a control inside it cannot know which
        /// tab it serves. The objects table stores its column layout per tab and the reports tab
        /// remembers its chart per tab, so both read the tab id from here.
        /// </remarks>
        /// <param name="view">The tab.</param>
        /// <param name="request">The request.</param>
        /// <returns>The binding payload.</returns>
        private static Dictionary<string, object> BuildBinding(InsightView view, IRequest request)
        {
            var table = CoreHub.GetUri<Table>()?
                .Add(new UriQuery("v", view.Id.ToString()))
                .BindParameters(request);

            return new Dictionary<string, object>
            {
                ["insighttable"] = table?.ToString(),
                ["insightview"] = view.Id.ToString()
            };
        }
    }
}
