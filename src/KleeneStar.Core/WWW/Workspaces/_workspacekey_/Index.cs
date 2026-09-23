using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebIcon;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Workspaces._workspacekey_
{
    /// <summary>
    /// Provides functionality for managing the current workspace page.
    /// </summary>
    [WebIcon<WorkspaceIcon>]
    [WorkspaceKeySegment]
    //[Policy<WorkspaceAdminPolicy>] - a page-level policy refuses everybody; see PageAuthorization
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="workspaceManager">
        /// The workspace manager used to retrieve workspace information. Cannot be null.
        /// </param>
        public Index(IWorkspaceManager workspaceManager)
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
            throw new RedirectException
            (
                CoreHub.GetUri<Issues._workspacekey_.Index>()
                    .BindParameters(renderContext.Request)
            );
        }
    }
}
