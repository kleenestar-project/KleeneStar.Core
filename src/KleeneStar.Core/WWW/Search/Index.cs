using KleeneStar.Core.WebFragment.Search;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Search
{
    /// <summary>
    /// Global search page — searches objects across every workspace. The search bar,
    /// results table, and pagination are contributed by the view fragments scoped to this
    /// page; the saved searches are contributed by the sidebar fragments.
    /// </summary>
    /// <remarks>
    /// When invoked with a <c>use</c> query parameter (the id of a saved search that was
    /// run, see <see cref="SavedSearchRun"/>), the page is titled by that saved search and, for
    /// its owner, stamps it as just used so the navigation dropdown's "recently used" ordering
    /// stays current.
    /// </remarks>
    [WebIcon<IconMagnifyingGlass>]
    [Title("kleenestar.core:search.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Title = "kleenestar.core:search.title";
            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:search.headline";

            // a saved search the caller may see names the page; one they may not see is not
            // run at all, so the page stays the plain search
            var request = renderContext?.Request;
            var savedSearch = SavedSearchRun.Resolve(request);

            if (savedSearch is null)
            {
                return;
            }

            visualTree.Title = savedSearch.Name;
            visualTree.Content.MainPanel.Headline.Title = savedSearch.Name;

            // the recently-used list is the owner's; running a search somebody shared does not
            // reorder theirs
            if (SavedSearchAuthorization.IsOwner(savedSearch, request))
            {
                CoreHub.SavedSearchManager.RecordUse(savedSearch.Id);
            }
        }
    }
}
