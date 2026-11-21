using WebExpress.WebApp.WebControl;
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

namespace KleeneStar.Core.WebFragment.Calendar
{
    /// <summary>
    /// Renders the calendar table inside <see cref="CalendarViewFragment"/>.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<CalendarViewFragment>]
    [Cache]
    public sealed class CalendarViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the REST-backed table control.
        /// </summary>
        public ControlDataTable Table { get; } = new()
        {
};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public CalendarViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the table is the view here rather than a block among others, so it takes the
            // height it is handed instead of growing with its rows: the rows then scroll under
            // a column header that stays, and the pager stays in reach below them
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.core:view.table.title";

            // declares the endpoint and, derived from its generic argument, the domain the
            // table serves, so the client subscribes to the change notification the CRUD
            // endpoint emits and the table refreshes after a create, update or delete.
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Calendars._classid_.Table>();

            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = CalendarViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = CalendarViewPaginationFragment.ContentId });

            Add(Table);
        }

        /// <summary>
        /// Converts the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
