using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Workflow._workflowid_
{
    /// <summary>
    /// Represents the main page for a workflow within the class.
    /// </summary>
    [WebIcon<IconWorkflow>]
    [Title("kleenestar.core:workflow.manage.label")]
    [WorkflowIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Workflow>]
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
            var workflowParameter = renderContext.Request.GetParameter<WorkflowIdParameter>();
            var guid = Guid.TryParse(workflowParameter?.Value, out var id) ? id : Guid.Empty;
            var workflow = CoreHub.WorkflowManager.GetWorkflow(guid);
            var @class = workflow?.Class;
            var workspace = workflow?.Class?.Workspace;

            // the id in the url is whatever the caller typed or kept in a bookmark, so it
            // may address a workflow that no longer exists. The breadcrumb is built from the
            // class the workflow belongs to and cannot be assembled without it; the page
            // then states that the workflow was not found instead of failing to render.
            if (workflow is null || @class is null)
            {
                visualTree.Title ??= I18N.Translate(renderContext, "kleenestar.core:workflow.notfound.title");
                visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:workflow.notfound.title");

                return;
            }

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("classes")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new ClassIdUriPathSegmentVariable<ClassIdParameter>()
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Class._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class.Id))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new WorkflowIdUriPathSegmentVariable<ClassIdParameter>()
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workflows._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class.Id))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("form")
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(new ClassIdParameter(@class.Id))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
            visualTree.Title ??= workflow?.Name;
            visualTree.Content.MainPanel.Headline.Title = workflow?.Name;
        }
    }
}
