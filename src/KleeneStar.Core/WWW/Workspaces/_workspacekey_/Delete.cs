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
    /// Represents the page for deleting a workspace within the web application. Provides access to the
    /// workspace edit form and handles form processing and rendering.
    /// </summary>
    [WebIcon<IconTrash>]
    [Title("kleenestar.core:workspace.delete.title")]
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Delete()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            // the workspace of the route is what the grant is administered on; the redirect a
            // refusal throws ends the page before the dialog's form is rendered
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(renderContext.Request.GetParameter<WorkspaceKeyParameter>()?.Value);

            PageAuthorization.Demand
            (
                renderContext.Request,
                typeof(WorkspaceDeletePermission),
                PageAuthorization.ChainOf(workspace)
            );
        }
    }
}
