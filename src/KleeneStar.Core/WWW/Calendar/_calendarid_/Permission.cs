using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Calendar._calendarid_
{
    /// <summary>
    /// Represents the dialog in which the permissions of a calendar are administered.
    /// </summary>
    /// <remarks>
    /// The page carries no content of its own; the surface is contributed by
    /// <see cref="WebFragment.Calendar.CalendarPermissionFragment"/>.
    /// </remarks>
    [WebIcon<IconUserShield>]
    [Title("kleenestar.core:calendar.permission.title")]
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
