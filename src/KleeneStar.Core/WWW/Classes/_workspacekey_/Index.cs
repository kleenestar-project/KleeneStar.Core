using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Classes._workspacekey_
{
    /// <summary>
    /// Represents the main class management page within the kleenestar web application.
    /// </summary>
    [WebIcon<IconClass>]
    [Title("kleenestar.core:class.manage.label")]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Class>]
    [Cache]
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
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            // class management is a subpage of its workspace — record the visit so the workspace
            // stays at the top of the dropdown's "recently used" list
            if (workspace is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                CoreHub.WorkspaceManager.RecordVisit(ownerId, workspace.Id);
            }

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()
                    .BindParameters(renderContext.Request)
                })
                .Concat(new ClassIdUriPathSegmentVariable<ClassIdParameter>()
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
        }
    }
}
