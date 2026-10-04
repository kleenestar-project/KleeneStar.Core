using System.Collections.Generic;
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
    /// Displays the list of all available insights in the sidebar of the insight view
    /// page, allowing the user to switch between insights.
    /// </summary>
    /// <remarks>
    /// The list used to sit on the home page, back when the home page was an insight. The
    /// home page is now the landing page (<see cref="WWW.Index"/>), which is the shared
    /// starting point rather than one person's board, so the switcher moved to where the
    /// boards are read.
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
            var list = new List<IHtmlNode>();

            foreach (var insight in CoreHub.InsightManager.GetInsights(new Query<Model.Entities.Insight>()))
            {
                list.Add(new ControlSidebarItemLink($"insight-{insight.Id}")
                {
                    Text = _ => insight.Name,
                    PrimaryAction = _ => new ActionFilter()
                    {
                        Exclusive = true,
                        Group = "insight"
                    }
                }
                    .Render(renderContext, visualTree));
            }

            return new HtmlList(list);
        }
    }
}
