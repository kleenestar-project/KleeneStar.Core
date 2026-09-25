using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Search
{
    /// <summary>
    /// Provides the results table of the global search page. The REST table is fed by the
    /// un-scoped object table endpoint and is bound to the search and pagination controls so
    /// it re-queries whenever the user types or pages.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<SearchViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class SearchViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the table that displays the objects matching the search across all workspaces.
        /// </summary>
        public ControlDataTable Table { get; } = new ColumnChoosingDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SearchViewTableFragment(IFragmentContext fragmentContext)
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
            //
            // while a saved search runs, the address names it: the endpoint then shows and
            // stores the columns of that saved search and applies its quickfilters. The service
            // is built while the page renders, which is when the request is at hand.
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Table>(descriptor =>
            {
                if (SavedSearchRun.Resolve(WebEx.CurrentRequest) is { } running)
                {
                    descriptor.WithBaseUri($"{descriptor.BaseUri}?{SavedSearchRun.Parameter}={running.Id}");
                }
            });

            // the query of a saved search being run - or a term carried over from the header
            // search box - seeds the first query, so the page opens on its results instead of on
            // every object; from there the search field above drives the table through the
            // binding below. The saved search wins, as it does in the search field.
            Table.StateFactory = renderContext =>
            {
                var request = renderContext?.Request;
                var wql = SavedSearchRun.ResolveWql(request);

                if (wql is not null)
                {
                    return DataState.Create().Set("wql", wql);
                }

                var query = request?.GetParameter(SearchViewSearchFragment.QueryParameter)?.Value;

                return !string.IsNullOrWhiteSpace(query) ? DataState.Create().Search(query) : null;
            };

            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = SearchViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = SearchViewPaginationFragment.ContentId });

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
