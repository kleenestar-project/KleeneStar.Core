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

namespace KleeneStar.Core.WWW.Fields._classid_
{
    /// <summary>
    /// Provides functionality for overview fields.
    /// </summary>
    [WebIcon<IconField>]
    [Title("kleenestar.core:field.manage.title")]
    [Description("kleenestar.core:field.manage.description")]
    [ClassIdSegment]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="classManager">
        /// The class manager used to retrieve class information. Cannot be null.
        /// </param>
        public Index(IClassManager classManager)
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var guid = Guid.TryParse(classParameter.Value, out var id) ? id : Guid.Empty;
            var @class = CoreHub.ClassManager.GetClass(guid);
            var workspace = @class?.Workspace;
            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = @class?.Workspace?.Key,
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
                .Concat(new UriPathSegmentConstant("fields")
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;

            // the class id comes from the url and may address a class that no longer
            // exists, so the headline falls back to the page title alone
            var page = I18N.Translate(renderContext, renderContext.PageContext.PageTitle);

            visualTree.Content.MainPanel.Headline.Title = @class is null ? page : $"{@class.Name} - {page}";
        }
    }
}
