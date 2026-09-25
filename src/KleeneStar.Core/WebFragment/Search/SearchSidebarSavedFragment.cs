using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Search
{
    // The entity type SavedSearch collides with the sibling WebFragment.SavedSearch
    // namespace; alias it (inside the namespace block) so the bare name binds to the entity.
    using SavedSearch = KleeneStar.Model.Entities.SavedSearch;

    /// <summary>
    /// Renders the saved searches of the calling identity into the global search page
    /// sidebar: a "new search" entry, the own saved searches (starred first) and the saved
    /// searches others shared with the caller. Each saved search
    /// runs on click and can be edited on double-click where the caller may change it.
    /// </summary>
    /// <remarks>
    /// The tooltip is the description as words (<see cref="ProseText"/>, it is a prose
    /// document), falling back to the query where nothing was written. The list is rendered
    /// once per page; a dialog that changes a saved search reloads the page
    /// (<c>Assets/js/savedsearch.js</c>).
    /// </remarks>
    [Section<SectionSidebarPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Search.Index>]
    [Cache]
    public sealed class SearchSidebarSavedFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SearchSidebarSavedFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Convert the fragment to HTML — the saved-search sidebar block.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node listing the saved-search sidebar items.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var running = SavedSearchRun.Resolve(request)?.Id;

            var nodes = new List<IHtmlNode>
            {
                // a search entry-point that clears any applied query
                new ControlSidebarItemLink("search-all")
                {
                    Text = _ => "kleenestar.core:search.sidebar.new.label",
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>()
                }
                    .Render(renderContext, visualTree),

                new ControlSidebarItemHeader("saved-header")
                {
                    Text = _ => "kleenestar.core:search.sidebar.saved.heading"
                }
                    .Render(renderContext, visualTree)
            };

            foreach (var savedSearch in CoreHub.SavedSearchManager.GetForOwner(ownerId))
            {
                nodes.Add(RenderItem(savedSearch, savedSearch.Starred, running, renderContext, visualTree));
            }

            // a new saved search is made from the search on screen, with the save button beside
            // the search field - an empty dialog here would only ask for the query the page
            // already shows
            var shared = CoreHub.SavedSearchManager.GetSharedWith(ownerId);

            if (shared.Count > 0)
            {
                nodes.Add(new ControlSidebarItemHeader("shared-header")
                {
                    Text = _ => "kleenestar.core:search.sidebar.shared.heading"
                }
                    .Render(renderContext, visualTree));

                foreach (var savedSearch in shared)
                {
                    // the star is the owner's pin, not the reader's
                    nodes.Add(RenderItem(savedSearch, false, running, renderContext, visualTree));
                }
            }

            return new HtmlList(nodes);
        }

        /// <summary>
        /// Renders one saved search as a sidebar link that runs it, with the edit dialog on
        /// double-click where the caller may change it.
        /// </summary>
        /// <param name="savedSearch">The saved search.</param>
        /// <param name="starred">Whether to mark it with the star.</param>
        /// <param name="running">The saved search the page runs, if any.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The rendered link.</returns>
        private static IHtmlNode RenderItem(SavedSearch savedSearch, bool starred, Guid? running, IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var editUri = SavedSearchAuthorization.MayUpdate(savedSearch, renderContext?.Request)
                ? CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Edit>()?
                    .BindParameters(new SavedSearchIdParameter(savedSearch.Id))
                : null;
            var description = ProseText.ToPlainText(savedSearch.Description);
            var tooltip = string.IsNullOrWhiteSpace(description) ? savedSearch.Query : description;

            // the link must not be bound against the request: on a page running a saved search
            // that would turn its `use` into the running one's
            return new UnboundSidebarItemLink($"ss-{savedSearch.Id}")
            {
                Text = _ => (starred ? "★ " : string.Empty) + savedSearch.Name,
                Tooltip = _ => tooltip,
                Uri = _ => SavedSearchRun.RunUri(savedSearch),
                Active = _ => savedSearch.Id == running ? TypeActive.Active : TypeActive.None,
                SecondaryAction = _ => editUri is null ? null : new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            }
                .Render(renderContext, visualTree);
        }
    }
}
