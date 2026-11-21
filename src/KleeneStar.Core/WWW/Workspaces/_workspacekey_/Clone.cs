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
    /// Represents the page for cloning a workspace within the web application. 
    /// </summary>
    [WebIcon<IconCopy>]
    [Title("kleenestar.core:workspace.clone.title")]
    [Scope<IScopeGeneral>]
    public sealed class Clone : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Clone()
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
                typeof(WorkspaceClonePermission),
                PageAuthorization.ChainOf(workspace)
            );
        }
    }
}
