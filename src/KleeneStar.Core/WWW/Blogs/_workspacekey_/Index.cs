using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Blogs._workspacekey_
{
    /// <summary>
    /// The blog overview of a workspace: the sidebar presents every object of the blog
    /// kind as a chronological timeline grouped by year and month, newest first
    /// (<see cref="WebFragment.Object.Blogs.BlogSidebarTimelineFragment"/>), while the
    /// main panel shows the stream of the latest posts
    /// (<see cref="WebFragment.Object.Blogs.BlogStreamFragment"/>). The sidebar also
    /// carries the kind links shared with the other kind overviews.
    /// </summary>
    [WebIcon<IconBlog>]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Object>]
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
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            // the breadcrumb shows the workspace (name and icon) beneath the application
            // root, mirroring the object detail page
            visualTree.BreadcrumbUri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                })
                .ToUri()
                .BindParameters(renderContext.Request);

            visualTree.Title = workspace?.Name;
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext.Request, "kleenestar.core:object.kind.blogs.label");

            // a kind overview is workspace content, so it advances the workspace's
            // "recently used" ranking just like the objects overview does
            if (workspace is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                _workspaceManager.RecordVisit(ownerId, workspace.Id);
            }
        }
    }
}
