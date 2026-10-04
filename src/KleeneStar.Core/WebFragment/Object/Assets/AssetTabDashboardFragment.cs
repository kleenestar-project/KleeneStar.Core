using KleeneStar.Core.WebFragment.Insight;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Provides the dashboard content of the asset overview, rendered inside the
    /// <see cref="AssetTabDashboardTemplateFragment"/> tab template. The fragment IS the
    /// dashboard control — it derives from <see cref="FragmentControlDataDashboard"/> and
    /// registers in <see cref="SectionTabTemplatePrimary"/>, the section the tab template
    /// collects its content from. Its data comes from the asset dashboard endpoint. The board
    /// is fully editable: columns and widgets can be added, renamed, resized, recolored,
    /// reordered, reconfigured and removed, all persisted through the endpoint.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<AssetTabDashboardTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class AssetTabDashboardFragment : FragmentControlDataDashboard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabDashboardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the board is the view here rather than a block among others, so it takes the
            // height it is handed instead of growing with its longest column: the widgets then
            // scroll below a menu bar that stays, so adding a column stays reachable
            Fill = _ => true;

            ServiceFactory = _ => DataServiceDescriptor.Data(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Dashboard>().ToString());

            // enable the full board editing surface; the endpoint persists every change and
            // reports which widget types the add menu may offer
            EditableColumn = _ => true;
            MovableColumn = _ => true;
            DeletableColumn = _ => true;
            AddableColumn = _ => true;
            AddableWidget = _ => true;
            ConfigurableWidget = _ => true;
        }

        /// <summary>
        /// Renders the control as an HTML node, first injecting the app widget registration
        /// script into the page head (see <see cref="DashboardWidgetScript"/>).
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var script = DashboardWidgetScript.Value;
            if (!string.IsNullOrEmpty(script))
            {
                visualTree.AddHeaderScript(script);
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
