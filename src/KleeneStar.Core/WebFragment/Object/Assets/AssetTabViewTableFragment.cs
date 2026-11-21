using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebMessageQueue;
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
    /// Table of the asset overview: a REST-backed table showing the workspace's assets,
    /// most recently updated first, bound to the search, quickfilter, and pagination
    /// controls of the view.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<AssetTabViewFragment>]
    [Cache]
    public sealed class AssetTabViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the rest table that displays the asset rows.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the table is the view here rather than a block among others, so it takes the
            // height it is handed instead of growing with its rows: the rows then scroll under
            // a column header that stays, and the pager stays in reach below them
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.core:view.table.title";

            // the endpoint is free-form and carries no generic argument, so the domain of the
            // objects this table lists is declared explicitly to get the same refresh behavior.
            //
            // while the page shows one class of the sidebar's class tree, the address names it,
            // so the endpoint lists that class and its descendants and offers their columns. The
            // service is built while the page renders, which is when the request is at hand.
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Table>
            (
                descriptor => descriptor
                    .WithDomain(DataChangedNotifier.DomainName(typeof(Model.Entities.Object)))
                    .WithBaseUri(ObjectClassFilter.Carry(descriptor.BaseUri, WebEx.CurrentRequest))
            );
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = AssetTabViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = AssetTabViewPaginationFragment.ContentId });

            Add(Table);
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
