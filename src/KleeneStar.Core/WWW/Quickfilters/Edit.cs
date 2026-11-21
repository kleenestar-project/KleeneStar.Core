using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Quickfilters
{
    /// <summary>
    /// Represents the dialog in which a quickfilter the user defined is changed.
    /// </summary>
    /// <remarks>
    /// The bar offers this from the chip's own menu and appends the filter's id to the address, so
    /// the dialog knows which filter it edits. The editor behind it is the application's, because
    /// what a filter selects is the application's business.
    /// </remarks>
    [WebIcon<IconFilter>]
    [Title("kleenestar.core:quickfilter.edit.title")]
    [Scope<IScopeGeneral>]
    public sealed class Edit : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Edit()
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
