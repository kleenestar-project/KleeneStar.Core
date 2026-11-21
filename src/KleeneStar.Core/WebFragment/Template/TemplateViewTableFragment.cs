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

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents a fragment control for managing template tables, providing functionality to
    /// render the fragment as HTML.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<TemplateViewFragment>]
    [Cache]
    public sealed class TemplateViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the table of control view items used to display template data.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the table is the view here rather than a block among others, so it takes the
            // height it is handed instead of growing with its rows: the rows then scroll under
            // a column header that stays, and the pager stays in reach below them
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.core:template.view.title";

            // declares the endpoint the table loads from. The domain it serves cannot be derived
            // from that endpoint — it composes the table rather than deriving from it, so it
            // carries no item type as a generic argument — and is therefore named here, so the
            // client still subscribes to the change notification the CRUD endpoint emits and the
            // table refreshes after a create, update or delete.
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_.Table>
            (
                descriptor => descriptor.WithDomain(DataChangedNotifier.DomainName(typeof(Model.Entities.Template)))
            );
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = TemplateViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = TemplateViewPaginationFragment.ContentId });

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
