using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a fragment control for managing workspace tiles, providing functionality to 
    /// render the fragment as HTML.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    //[Policy<WorkspaceViewPolicy>]
    [Scope<WorkspaceViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class WorkspaceViewTileFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the configuration tile that provides REST access to 
        /// workspace data.
        /// </summary>
        public ControlDataTile Tile { get; } = new ControlDataTile()
        {
            ServiceFactory = _ => DataServiceDescriptor.Data(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Tile>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceViewTileFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the tiles are the view here rather than a block among others, so they take the
            // height they are handed instead of growing with their number: the tiles then
            // scroll above chrome that stays instead of pushing the pager out of reach
            Tile.Fill = _ => true;

            Icon = _ => new IconTile();
            Title = _ => "kleenestar.core:view.tile.title";
            Tile.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = WorkspaceViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = WorkspaceViewPaginationFragment.ContentId });

            Add(Tile);
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
            return base.Render(renderContext, visualTree);
        }
    }
}
