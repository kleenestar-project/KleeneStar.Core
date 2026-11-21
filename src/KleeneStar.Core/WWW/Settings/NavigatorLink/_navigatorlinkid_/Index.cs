using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.NavigatorLink._navigatorlinkid_
{
    /// <summary>
    /// Declares the id segment of the navigator link routes.
    /// </summary>
    /// <remarks>
    /// The variable segment of this folder is only bound to a parameter when a page inside it says
    /// so. Without this declaration the sibling pages keep the literal placeholder in their address,
    /// and every row option of the settings table would open the form of no particular link.
    /// </remarks>
    [SegmentGuid<NavigatorLinkIdParameter>]
    [WebIcon<IconLink>]
    [Title("kleenestar.core:setting.navigatorlink.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScope
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
