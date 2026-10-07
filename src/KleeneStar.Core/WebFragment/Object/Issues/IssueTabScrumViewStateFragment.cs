using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Owns the shared query state of the scrum view: one
    /// <see cref="ControlViewState"/> declaring the board and the backlog as two resources
    /// over one search term and one quickfilter selection.
    /// </summary>
    /// <remarks>
    /// It sits in the view header rather than inside one of the views on purpose. The search
    /// (<see cref="IssueTabScrumViewSearchFragment"/>) and the quickfilter
    /// (<see cref="IssueTabScrumViewQuickfilterFragment"/>) are header controls shown for
    /// both views, so the state they write must outlive a view switch — and both view items
    /// must be able to read it. The header is the one place that is neither.
    ///
    /// Neither the search nor the quickfilter needs to be a descendant of this host: a
    /// control resolves its ViewState by the resource it binds to, because the registry
    /// indexes every ViewState by the resources it declares. DOM ancestry is only the
    /// fallback.
    ///
    /// The state paths are the ones <see cref="DataQueryState"/> defines; each resource maps
    /// them onto the query parameters its endpoint reads (<c>q</c> for the search, <c>f</c>
    /// for the quickfilter chips).
    /// </remarks>
    [Section<SectionViewHeaderPreferences>]
    [Scope<IssueTabScrumViewFragment>]
    [Cache]
    public sealed class IssueTabScrumViewStateFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumViewStateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            Add(new global::KleeneStar.Core.WebControl.TabBoardViewState(id + "-viewstate")
                .State(_ => { })
                .Service<WWW.Api._1_.Objects._workspacekey_.ScrumSprintKanban>(service => service.Method(HttpMethod.Get))
                .Service<WWW.Api._1_.Objects._workspacekey_.ScrumBacklog>(service => service.Method(HttpMethod.Get))
                .Resource<IssueSprintBoardResource>(resource => resource
                    .Service<WWW.Api._1_.Objects._workspacekey_.ScrumSprintKanban>()
                    .Param("f", "filter")
                    .Param("q", "search"))
                .Resource<IssueScrumBacklogResource>(resource => resource
                    .Service<WWW.Api._1_.Objects._workspacekey_.ScrumBacklog>()
                    .Param("f", "filter")
                    .Param("q", "search")));
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
