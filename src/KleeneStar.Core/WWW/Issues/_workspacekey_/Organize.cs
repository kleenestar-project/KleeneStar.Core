using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issues._workspacekey_
{
    /// <summary>
    /// Modal page that hosts the workspace object tree in which objects can be re-parented via
    /// drag and drop. The <c>${workspacekey}</c> URL segment is declared by the sibling
    /// <see cref="Index"/> page, so this page carries no segment attribute of its own. The tree
    /// itself is supplied by <c>WorkspaceObjectsOrganizeFragment</c> scoped to this page.
    /// </summary>
    [WebIcon<IconSitemap>]
    [Title("kleenestar.core:workspace.organize.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Organize : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Organize()
        {
        }

        /// <summary>
        /// Processing of the resource. The content is contributed entirely by the scoped
        /// organize fragment, so no work is required here.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
