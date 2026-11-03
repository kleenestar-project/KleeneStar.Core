using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Insights
{
    /// <summary>
    /// Represents the dialog in which the installation's administrators say which groups may
    /// create insights.
    /// </summary>
    /// <remarks>
    /// The page carries no content of its own; the surface is contributed by
    /// <see cref="WebFragment.Insight.InsightCreationPermissionFragment"/>. It is guarded by
    /// <see cref="WebPermission.RouteAuthorization"/> (administrators only).
    /// </remarks>
    [WebIcon<IconUserShield>]
    [Title("kleenestar.core:insight.creation.permission.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Permission : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Permission()
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
