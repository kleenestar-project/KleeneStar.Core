using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Search
{
    /// <summary>
    /// The list view of the global search page: the results as a list, with the selected object
    /// shown beside it in a master-detail pane.
    /// </summary>
    /// <remarks>
    /// It is the second presentation of the same result as the table - bound to the same search
    /// field, quickfilter bar and pager, and seeded with the same query - so the view switch
    /// changes how the result is shown, not what it is.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<SearchViewFragment>]
    [Order(2)]
    [Cache]
    public sealed class SearchViewListFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the master-detail control: the result list and the pane of the selected object.
        /// </summary>
        public ListDetailControl List { get; } = new ListDetailControl()
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.List>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SearchViewListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconList();
            Title = _ => "kleenestar.core:view.list.title";

            // the query the page opens with - a saved search's, or a term from the header - so
            // the list starts on the same result as the table
            List.List.StateFactory = renderContext =>
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

            List.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = SearchViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = SearchViewPaginationFragment.ContentId });

            Add(List);
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
