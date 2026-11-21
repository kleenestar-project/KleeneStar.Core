using KleeneStar.Core.WebRestApi;
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

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Represents a fragment that renders objects in a tile/card view with search,
    /// filtering, and pagination support. Rendered as a view item inside the
    /// <see cref="AssetTabViewFragment"/> tab template.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<AssetTabViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class AssetTabViewTileFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the tile control rendering the objects as cards.
        /// </summary>
        public ControlDataTile Tile { get; } = new ControlDataTile()
        {
            // narrowed to the class the page shows, when it shows one of the sidebar's class tree
            ServiceFactory = renderContext => DataServiceDescriptor.Data(ObjectClassFilter.Carry
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Tile>().ToString(),
                renderContext?.Request
            ))
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabViewTileFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the tiles are the view here rather than a block among others, so they take the
            // height they are handed instead of growing with their number: the tiles then
            // scroll above chrome that stays instead of pushing the pager out of reach
            Tile.Fill = _ => true;

            Icon = _ => new IconTile();
            Title = _ => "kleenestar.core:view.tile.title";
            Tile.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = AssetTabViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = AssetTabViewPaginationFragment.ContentId });

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
