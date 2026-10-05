using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Builds the query surface the tabs of an insight share - the search above a view and the
    /// quickfilter bar beside it - so each tab template composes the same controls the issue
    /// overview does, pointed at the insight's endpoints.
    /// </summary>
    internal static class InsightViewControls
    {
        /// <summary>
        /// Builds a search over the object attributes, writing its term into the view state of a
        /// board, plan or calendar resource.
        /// </summary>
        /// <typeparam name="TResource">The resource the search narrows.</typeparam>
        /// <param name="id">The element id, unique on the page.</param>
        /// <returns>The search.</returns>
        public static ControlAdvancedSearch BuildSearch<TResource>(string id)
            where TResource : IDataResource
        {
            var search = BuildSearch(id);

            // the cast picks the writing-surface overload of Resource<T> - without it the compiler
            // settles on the ControlDataList one and fails on the receiver type
            ((IViewStateModelBound)search).Resource<TResource>().Model("search");

            return search;
        }

        /// <summary>
        /// Builds a search over the object attributes.
        /// </summary>
        /// <param name="id">The element id, unique on the page.</param>
        /// <returns>The search.</returns>
        public static ControlAdvancedSearch BuildSearch(string id)
        {
            return new ControlAdvancedSearch(id)
            {
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Wql>().ToString())
            };
        }

        /// <summary>
        /// Builds the quickfilter bar of a board, plan or calendar, writing the chips into the
        /// view state of its resource.
        /// </summary>
        /// <typeparam name="TResource">The resource the chips narrow.</typeparam>
        /// <param name="id">The element id, unique on the page.</param>
        /// <returns>The quickfilter bar.</returns>
        public static ControlDataQuickfilter BuildBoardQuickfilter<TResource>(string id)
            where TResource : IDataResource
        {
            var quickfilter = BuildQuickfilter<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.BoardQuickfilter>(id);

            ((IViewStateModelBound)quickfilter).Resource<TResource>().Model("filter");

            return quickfilter;
        }

        /// <summary>
        /// Builds a quickfilter bar served by an insight quickfilter endpoint, with the dialogs
        /// that add and edit the filters users define on the insight.
        /// </summary>
        /// <typeparam name="TEndpoint">The quickfilter endpoint.</typeparam>
        /// <param name="id">The element id, unique on the page.</param>
        /// <returns>The quickfilter bar.</returns>
        public static ControlDataQuickfilter BuildQuickfilter<TEndpoint>(string id)
            where TEndpoint : InsightQuickfilterBase
        {
            var quickfilter = new ControlDataQuickfilter(id)
            {
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<TEndpoint>().ToString()),
                EditAction = renderContext => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Edit>()
                        .BindParameters(renderContext.Request)
                        .Concat(DialogQuery(renderContext)),
                    TypeModalSize.Large
                )
            };

            quickfilter.Add(new ControlQuickfilterItemAdd()
            {
                Tooltip = _ => "kleenestar.core:quickfilter.add.label",
                PrimaryAction = renderContext => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Add>()
                        .BindParameters(renderContext.Request)
                        .Concat(DialogQuery(renderContext)),
                    TypeModalSize.Large
                )
            });

            return quickfilter;
        }

        /// <summary>
        /// Returns the query the quickfilter dialogs are addressed with: the view key of insights
        /// and the insight of the page as the context.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The query string.</returns>
        private static string DialogQuery(IRenderControlContext renderContext)
        {
            var insightId = renderContext.Request?.GetParameter<InsightIdParameter>()?.Value;

            return $"?view={InsightScope.QuickfilterView}&context={insightId}";
        }
    }
}
