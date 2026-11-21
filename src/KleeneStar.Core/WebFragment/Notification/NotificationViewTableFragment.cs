using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Notification
{
    /// <summary>
    /// The table of the notification center, bound to the search, the quick filters and the
    /// pagination of the surrounding view.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<NotificationViewFragment>]
    [Cache]
    public sealed class NotificationViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the table showing the notifications.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public NotificationViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the table is the view here rather than a block among others, so it takes the
            // height it is handed instead of growing with its rows: the rows then scroll under
            // a column header that stays, and the pager stays in reach below them
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.core:view.table.title";

            // declares the endpoint and, derived from its generic argument, the domain the
            // table serves, so the client subscribes to the change notification the endpoint
            // emits and the table refreshes when a notification arrives or is read
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Notifications.Table>();
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = NotificationViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = NotificationViewPaginationFragment.ContentId });

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
