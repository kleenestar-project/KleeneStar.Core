using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Sla._slaid_
{
    /// <summary>
    /// Details page for a single <see cref="Model.Entities.SlaPolicy"/>.
    /// </summary>
    [WebIcon<IconClock>]
    [Title("kleenestar.core:sla.detail.title")]
    [SlaIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.SlaPolicy>]
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
            var slaParameter = renderContext.Request.GetParameter<SlaIdParameter>();
            var guid = Guid.TryParse(slaParameter?.Value, out var id) ? id : Guid.Empty;
            var policy = CoreHub.SlaManager.GetSla(guid);
            var @class = policy?.Class;
            var workspace = @class?.Workspace;

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
                    Value = @class?.Id.ToString(),
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Class._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("slas")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Slas._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new SlaIdUriPathSegmentVariable<SlaIdParameter>()
                {
                    Value = policy?.Id.ToString(),
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
            visualTree.Title ??= policy?.Name;
            visualTree.Content.MainPanel.Headline.Title = policy?.Name;
        }
    }
}
