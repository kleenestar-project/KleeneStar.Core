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

namespace KleeneStar.Core.WWW.Relations._classid_
{
    /// <summary>
    /// The relation administration of a class: which relations objects of the class may hold,
    /// how each reads from either end, which classes it accepts as a target, how often it may
    /// meet at each end and what it does to the workflow.
    /// </summary>
    /// <remarks>
    /// The page sits beside the field, form, status, workflow, priority and SLA administration
    /// of a class, because a relation is configuration of the same kind: it is defined once and
    /// then offered wherever an object of the class is read. The surface itself is contributed
    /// by <see cref="WebFragment.Class.ClassRelationEditorFragment"/>.
    /// </remarks>
    [WebIcon<IconLink>]
    [Title("kleenestar.core:relation.manage.title")]
    [Description("kleenestar.core:relation.manage.description")]
    [ClassIdSegment]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IClassManager _classManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="classManager">
        /// The class manager used to retrieve class information. Cannot be null.
        /// </param>
        public Index(IClassManager classManager)
        {
            _classManager = classManager;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var classParameter = renderContext.Request.GetParameter<ClassIdParameter>();
            var guid = Guid.TryParse(classParameter?.Value, out var id) ? id : Guid.Empty;
            var @class = _classManager.GetClass(guid);
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
                .Concat(new UriPathSegmentConstant("relations")
                {
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;

            // the class id comes from the url and may address a class that no longer exists,
            // so the headline falls back to the page title alone
            var page = I18N.Translate(renderContext, renderContext.PageContext.PageTitle);

            visualTree.Content.MainPanel.Headline.Title = @class is null ? page : $"{@class.Name} - {page}";
        }
    }
}
