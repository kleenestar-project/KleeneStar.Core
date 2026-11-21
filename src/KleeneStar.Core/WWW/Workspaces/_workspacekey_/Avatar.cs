using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Workspaces._workspacekey_
{
    /// <summary>
    /// Represents the page for editing a workspace within the web application. 
    /// Provides access to the workspace edit form and handles form processing and rendering.
    /// </summary>
    [WebIcon<IconPencil>]
    [Title("kleenestar.core:workspace.avatar.title")]
    //[Policy<WorkspaceAdminPolicy>] - a page-level policy refuses everybody; see PageAuthorization
    [Scope<IScopeGeneral>]
    public sealed class Avatar : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Avatar()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            // the picture is part of the workspace, so changing it asks what editing it asks
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(renderContext.Request.GetParameter<WorkspaceKeyParameter>()?.Value);

            PageAuthorization.Demand
            (
                renderContext.Request,
                typeof(WorkspaceUpdatePermission),
                PageAuthorization.ChainOf(workspace)
            );
        }
    }
}
