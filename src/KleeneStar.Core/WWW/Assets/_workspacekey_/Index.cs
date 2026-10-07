using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Assets._workspacekey_
{
    /// <summary>
    /// The asset overview of a workspace: a curated, filterable list of the objects of
    /// the asset kind, most recently updated first, with search, personal quickfilters
    /// (starred, assigned to me, created by me, archived), and pagination — contributed
    /// by the standalone <see cref="WebFragment.Object.Assets.AssetTabViewFragment"/>. The
    /// sidebar carries the kind links shared with the other kind overviews and the asset
    /// classes as an inheritance tree; a class picked there travels as <c>?class=&lt;id&gt;</c>
    /// and narrows the table, list and tile views to that class and its descendants
    /// (<see cref="WebRestApi.ObjectClassFilter"/>).
    /// </summary>
    [WebIcon<IconCubes>]
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
            // root, mirroring the other kind overviews
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
            // narrowed to a class of the sidebar's class tree, the overview is titled by it
            var @class = ObjectClassFilter.Resolve(renderContext.Request);

            visualTree.Content.MainPanel.Headline.Title = @class is not null
                ? @class.Name
                : I18N.Translate(renderContext.Request, "kleenestar.core:object.kind.assets.label");

            // a kind overview is workspace content, so it advances the workspace's
            // "recently used" ranking just like the other kind overviews do
            if (workspace is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                _workspaceManager.RecordVisit(ownerId, workspace.Id);
            }
        }
    }
}
