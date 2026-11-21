using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
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

namespace KleeneStar.Core.WWW.Calendars._classid_
{
    /// <summary>
    /// Calendar overview page for a class. Lists every <see cref="Model.Entities.Calendar"/>
    /// bound to the class addressed by the URL <c>classid</c> segment.
    /// </summary>
    [WebIcon<IconCalendar>]
    [Title("kleenestar.core:calendar.manage.title")]
    [Description("kleenestar.core:calendar.manage.description")]
    [ClassIdSegment]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="classManager">The class manager.</param>
        public Index(IClassManager classManager)
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var guid = Guid.TryParse(classParameter?.Value, out var id) ? id : Guid.Empty;
            var @class = CoreHub.ClassManager.GetClass(guid);
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
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Class._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("calendars")
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;

            visualTree.Content.MainPanel.Headline.Title = @class is null
                ? I18N.Translate(renderContext, renderContext.PageContext.PageTitle)
                : $"{@class.Name} - {I18N.Translate(renderContext, renderContext.PageContext.PageTitle)}";
        }
    }
}
