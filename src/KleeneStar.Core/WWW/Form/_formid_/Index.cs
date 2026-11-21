using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Form._formid_
{
    /// <summary>
    /// Represents the main page for a form within the class.
    /// </summary>
    [WebIcon<IconListFunction>]
    [Title("kleenestar.core:form.manage.label")]
    [FormIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Form>]
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
            var formParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var guid = Guid.TryParse(formParameter?.Value, out var id) ? id : Guid.Empty;
            var form = CoreHub.FormManager.GetForm(guid);
            var @class = form?.Class;
            var workspace = form?.Class?.Workspace;

            // the id in the url is whatever the caller typed or kept in a bookmark, so it
            // may address a form that no longer exists. The breadcrumb is built from the
            // class the form belongs to and cannot be assembled without it; the page then
            // states that the form was not found instead of failing to render at all.
            if (form is null || @class is null)
            {
                visualTree.Title ??= I18N.Translate(renderContext, "kleenestar.core:form.notfound.title");
                visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:form.notfound.title");

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
                .Concat(new FormIdUriPathSegmentVariable<ClassIdParameter>()
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Forms._classid_.Index>()
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
            visualTree.Title ??= form?.Name;
            visualTree.Content.MainPanel.Headline.Title = form?.Name;
        }
    }
}
