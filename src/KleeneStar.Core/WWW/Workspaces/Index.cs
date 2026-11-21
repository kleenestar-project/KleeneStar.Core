using KleeneStar.Core.WebIcon;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Workspaces
{
    /// <summary>
    /// Represents the main workspace management page within the kleenestar web application.
    /// </summary>
    [WebIcon<WorkspaceIcon>]
    [Title("kleenestar.core:workspace.manage.title")]
    [Scope<IScopeGeneral>]
    // the overview lists every workspace and is administered on none of them; the rows a caller
    // may not read are the concern of the list it embeds. A page-level policy attribute would ask
    // the framework's global group question instead (see WebPermission.PageAuthorization)
    [Domain<Workspace>]
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
        }
    }
}
