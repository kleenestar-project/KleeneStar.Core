using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Lists the insights in the sidebar of the insight page, so the reader can switch
    /// between them without going back to the overview.
    /// </summary>
    /// <remarks>
    /// The list is the one the header menu offers: the active insights the caller may read
    /// (<see cref="ContentVisibility.Restrict(IQuery{Model.Entities.Insight})"/>), by name. It
    /// used to list every insight of the installation - names the caller was refused included -
    /// as filter entries that did nothing on this page. Each entry is now a link to its insight
    /// page, the open one marked. The links are unbound: a bound link would have its
    /// <c>{insightid}</c> replaced by the one of the page being shown.
    /// </remarks>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    [Cache]
    public sealed class InsightSidebarFilterFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its
        /// operation. Cannot be null.
        /// </param>
        public InsightSidebarFilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var current = InsightScope.Resolve(request)?.Id;
            var list = new List<IHtmlNode>();

            foreach (var insight in GetListed())
            {
                var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Insight._insightid_.Index>()?
                    .BindParameters(new InsightIdParameter(insight.Id));

                list.Add(new UnboundSidebarItemLink($"insight-{insight.Id}")
                {
                    Text = _ => insight.Name,
                    Uri = _ => uri,
                    Active = _ => insight.Id == current ? TypeActive.Active : TypeActive.None
                }
                    .Render(renderContext, visualTree));
            }

            return new HtmlList(list);
        }

        /// <summary>
        /// Returns the insights the sidebar lists for the caller: active, readable, by name.
        /// </summary>
        /// <returns>The insights to list.</returns>
        internal static IReadOnlyList<Model.Entities.Insight> GetListed()
        {
            return [.. CoreHub.InsightManager
                .GetInsights(ContentVisibility.Restrict(new Query<Model.Entities.Insight>()))
                .Where(x => x.State.IsActive())
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }
}
