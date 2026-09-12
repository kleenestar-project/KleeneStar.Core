using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Workspaces._workspacekey_
{
    /// <summary>
    /// Represents the permissions management modal page for a workspace.
    /// Displays the workspace name in the header and provides the permission profile assignment interface.
    /// </summary>
    [WebIcon<IconUserShield>]
    [Title("kleenestar.core:workspace.permissions.title")]
    [Scope<IScopeGeneral>]
    public sealed class Permissions : IPage<VisualTreeWebApp>, IScope
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="workspaceManager">
        /// The workspace manager used to retrieve workspace information. Cannot be null.
        /// </param>
        public Permissions(IWorkspaceManager workspaceManager)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            // administering who may do what in a workspace is the workspace's own grant; the
            // redirect a refusal throws ends the page before the assignment surface is rendered
            PageAuthorization.Demand
            (
                renderContext.Request,
                typeof(WorkspaceManageProfilesPermission),
                PageAuthorization.ChainOf(workspace)
            );

            // display workspace name in the modal header
            if (workspace != null)
            {
                var title = string.Format(
                    I18N.Translate(renderContext, "kleenestar.core:workspace.permissions.header"),
                    workspace.Name
                );

                visualTree.Title = title;
            }
        }
    }
}
