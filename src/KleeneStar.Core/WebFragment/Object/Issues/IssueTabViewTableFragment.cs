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

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Table of the issue overview: a REST-backed table showing the workspace's issues,
    /// bound to the search, quickfilter, and pagination controls of the view.
    /// </summary>
    /// <remarks>
    /// Which columns the table shows is the choice of the user looking at it, made in the
    /// column manager behind the ≡ button of the header row and stored per identity and
    /// per view by the endpoint. The endpoint tells the views apart by a <c>v</c>
    /// parameter in its address, which the tab control writes into the data service of
    /// this table when it instantiates the template for a view (see the
    /// <see cref="BindTemplate"/> below and <c>Objects/${workspacekey}/Tab</c>): the
    /// template is rendered once on the server and cloned into a pane per view, so the
    /// address cannot carry the view already at render time.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<IssueTabViewFragment>]
    [Cache]
    public sealed class IssueTabViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// The selector the view-scoped endpoint address is written to: the data service
        /// island of this table. A pane hosts the service islands of its list, tile and
        /// pagination controls as well, so the selector names the table's own.
        /// </summary>
        private const string ServiceIslandSelector = ".wx-webapp-table > wx-service";

        /// <summary>
        /// Gets the rest table that displays the issue rows.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabViewTableFragment(IFragmentContext fragmentContext)
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
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Table>
            (
                descriptor => descriptor.WithDomain(DataChangedNotifier.DomainName(typeof(Model.Entities.Object)))
            );
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = IssueTabViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = IssueTabViewPaginationFragment.ContentId })
                .Add(new BindTemplate().Add
                (
                    "issuetable",
                    TypeBindMode.Attr,
                    target: ServiceIslandSelector,
                    name: "base-uri"
                ));

            Add(Table);
        }


    }
}
