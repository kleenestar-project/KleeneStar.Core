using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Workspaces._workspacekey_
{
    /// <summary>
    /// Toggles the calling identity's favorite flag on the addressed workspace and redirects
    /// back to the workspace management list. The URL is <c>/workspaces/{workspacekey}/favorite</c>;
    /// the <c>{workspacekey}</c> segment is declared by the sibling <see cref="Index"/> page, so
    /// this sibling must NOT redeclare it.
    /// </summary>
    /// <remarks>
    /// The toggle is reached from the workspace management table's overflow menu, whose label
    /// already reflects the current state, so a single navigating link is enough: opening the page
    /// flips the favorite and the subsequent redirect re-renders the list (and the workspace
    /// dropdown) with the new state. Persistence and the confirmation toast are owned by
    /// <see cref="IWorkspaceManager.SetFavorite"/>.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Favorite : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace and toggle the favorite. Cannot be null.
        /// </param>
        public Favorite(IWorkspaceManager workspaceManager)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Processing of the resource: flips the favorite flag of the addressed workspace for the
        /// calling identity, then redirects to the workspace management list.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            // the page acts and redirects before the page guard renders, so it asks itself
            WebPermission.RouteAuthorization.Demand(renderContext);

            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            if (workspace is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                var isFavorite = _workspaceManager.IsFavorite(ownerId, workspace.Id);
                _workspaceManager.SetFavorite(ownerId, workspace.Id, !isFavorite);
            }

            throw new RedirectException
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces.Index>()
            );
        }
    }
}
