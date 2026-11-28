using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_
{
    /// <summary>
    /// The page of a template hierarchy. Declaring the hierarchy-id segment here turns the folder
    /// into a route variable for the dialogs below it; the page itself leads to the hierarchy
    /// overview of the hierarchy's workspace.
    /// </summary>
    [WebIcon<IconSitemap>]
    [Title("kleenestar.core:template.hierarchy.manage.label")]
    [TemplateHierarchyIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.TemplateHierarchy>]
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
        /// Leads to the hierarchy overview of the hierarchy's workspace.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var value = renderContext.Request.GetParameter<TemplateHierarchyIdParameter>()?.Value;
            var hierarchy = Guid.TryParse(value, out var id) ? CoreHub.TemplateHierarchyManager.GetHierarchy(id) : null;
            var key = hierarchy?.Workspace?.Key ?? (hierarchy is null ? null : CoreHub.WorkspaceManager.GetWorkspace(hierarchy.WorkspaceId)?.Key);

            throw new RedirectException
            (
                string.IsNullOrWhiteSpace(key)
                    ? CoreHub.GetUri<global::KleeneStar.Core.WWW.Index>()
                    : CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Hierarchies>()
                        .BindParameters(new WorkspaceKeyParameter(key))
            );
        }
    }
}
