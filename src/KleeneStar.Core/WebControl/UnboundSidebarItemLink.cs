using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A sidebar link that leads exactly where it was told, without re-binding its address
    /// against the request the page was rendered for.
    /// </summary>
    /// <remarks>
    /// <see cref="ControlSidebarItemLink"/> passes its address through
    /// <c>BindParameters(request)</c>, which replaces not only variable path segments but also
    /// the value of every query key the request carries too. On the search page, which runs a
    /// saved search as <c>?use=&lt;id&gt;</c>, that turned the link of every saved search in the
    /// sidebar into a link to the one already running. The rendered address is put back to the
    /// one the link was given.
    /// </remarks>
    /// <param name="id">The control id.</param>
    public class UnboundSidebarItemLink(string id = null) : ControlSidebarItemLink(id)
    {
        /// <summary>
        /// Converts the control to an HTML representation, with the address as given.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <param name="text">The text to display for the link.</param>
        /// <param name="tooltip">The tooltip text to display on hover.</param>
        /// <param name="uri">The URI to navigate to when the link is clicked.</param>
        /// <param name="icon">The icon to display alongside the link text.</param>
        /// <param name="primaryAction">The primary action to execute when the link is clicked.</param>
        /// <param name="secondaryAction">The secondary action to execute on a double-click.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree, string text, string tooltip, IUri uri, IIcon icon, IAction primaryAction, IAction secondaryAction)
        {
            var html = base.Render(renderContext, visualTree, text, tooltip, uri, icon, primaryAction, secondaryAction);

            if (uri is not null && html is HtmlElement element)
            {
                element.AddUserAttribute("data-uri", uri.ToString());
            }

            return html;
        }
    }
}
