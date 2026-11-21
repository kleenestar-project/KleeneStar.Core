using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_
{
    /// <summary>
    /// The dialog that deletes a template hierarchy (<see cref="WebFragment.TemplateHierarchy.TemplateHierarchyDeleteFormFragment"/>).
    /// </summary>
    [WebIcon<IconTrash>]
    [Title("kleenestar.core:template.hierarchy.delete.title")]
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Processes the page; the delete form fragment renders the content.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
