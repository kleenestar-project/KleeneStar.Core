using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Templates._workspacekey_
{
    /// <summary>
    /// The dialog that adds a template hierarchy to the workspace
    /// (<see cref="WebFragment.TemplateHierarchy.TemplateHierarchyAddFormFragment"/>).
    /// </summary>
    [WebIcon<IconPlus>]
    [Title("kleenestar.core:template.hierarchy.add.label")]
    [Scope<IScopeGeneral>]
    public sealed class AddHierarchy : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Processes the page; the wizard fragment renders the content.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
