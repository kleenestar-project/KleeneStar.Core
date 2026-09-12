using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Workspaces
{
    /// <summary>
    /// Represents a page that provides a form for adding a new workspace within the application.
    /// </summary>
    [WebIcon<IconPlus>]
    [Title("kleenestar.core:workspace.add.label")]
    // creating a workspace is administered on nothing this application has a grant for: the
    // permission chain starts at the workspace and there is none yet. Until an installation-level
    // resource exists the page is open, and a page-level policy attribute would not help (see
    // WebPermission.PageAuthorization for why none is active)
    [Scope<IScopeGeneral>]
    public sealed class Add : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Add()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
