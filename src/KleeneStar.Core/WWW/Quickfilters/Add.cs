using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Quickfilters
{
    /// <summary>
    /// Represents the dialog in which a user defines a new quickfilter for the bar it was opened
    /// from.
    /// </summary>
    /// <remarks>
    /// The framework raises an edit-filter event and leaves the editor to the application, because
    /// what a filter selects is the application's business. This is that editor. It is opened by
    /// the add chip's action rather than by a listener on the event, because a listener would have
    /// to be shipped as a script of this plugin and such a script cannot currently be addressed
    /// (see the include path defect noted with the maintenance work).
    ///
    /// Which bar the filter is destined for travels in the <c>view</c> and <c>context</c> query
    /// parameters the chip puts on this address.
    /// </remarks>
    [WebIcon<IconFilter>]
    [Title("kleenestar.core:quickfilter.add.title")]
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
