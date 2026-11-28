using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Templates._workspacekey_
{
    /// <summary>
    /// Provides the overview of the template hierarchies of a workspace
    /// (<c>/templates/{workspacekey}/hierarchies</c>). The templates have their own overview
    /// (<see cref="Index"/>); the sidebar of both pages links them.
    /// </summary>
    /// <remarks>
    /// The page lives in the template area, so <see cref="WebPermission.RouteAuthorization"/>
    /// demands the administration of the workspace's classes as it does for the templates.
    /// </remarks>
    [WebIcon<IconSitemap>]
    [Title("kleenestar.core:template.hierarchy.manage.title")]
    [Description("kleenestar.core:template.hierarchy.manage.description")]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.TemplateHierarchy>]
    [Cache]
    public sealed class Hierarchies : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the route's workspace key.
        /// </param>
        public Hierarchies(IWorkspaceManager workspaceManager)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Processes the hierarchy overview: sets the breadcrumb workspace, templates, hierarchies.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("templates")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>()
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("hierarchies")
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
        }
    }
}
