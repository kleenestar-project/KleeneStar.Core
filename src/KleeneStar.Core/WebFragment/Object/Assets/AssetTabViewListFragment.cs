using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Provides a list view fragment for displaying objects with integrated search,
    /// filtering, and pagination capabilities. Rendered as a view item inside the
    /// <see cref="AssetTabViewFragment"/> tab template and as the content of the
    /// standalone tab template.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<AssetTabViewFragment>]
    [Order(2)]
    [Cache]
    public sealed class AssetTabViewListFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the list control rendering the objects as a vertical frame list. 
        /// </summary>
        public ListDetailControl List { get; } = new ListDetailControl()
        {
            // narrowed to the class the page shows, when it shows one of the sidebar's class tree
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData(ObjectClassFilter.Carry
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.List>().ToString(),
                renderContext?.Request
            ))
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabViewListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconList();
            Title = _ => "kleenestar.core:view.list.title";
            List.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = AssetTabViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = AssetTabViewPaginationFragment.ContentId });

            Add(List);
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
