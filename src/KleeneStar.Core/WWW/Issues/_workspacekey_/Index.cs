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

namespace KleeneStar.Core.WWW.Issues._workspacekey_
{
    /// <summary>
    /// The issue overview of a workspace: a tab control
    /// (<see cref="WebFragment.Object.Issues.IssueTabFragment"/>) hosting the workspace's
    /// persisted views. The leading issues tab lists the objects of the issue kind,
    /// most recently updated first, with search, quickfilters (starred, assigned to
    /// me, created by me, archived), and pagination; further user-defined views
    /// (table, list, dashboard, Kanban, Scrum) can be added, reordered, and removed
    /// via the template picker. The sidebar carries the kind links shared with the
    /// other kind overviews.
    /// </summary>
    [WebIcon<IconListCheck>]
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
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext.Request, "kleenestar.core:object.kind.issues.label");

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
