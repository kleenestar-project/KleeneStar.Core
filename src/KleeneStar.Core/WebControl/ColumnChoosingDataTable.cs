using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A REST table that always offers its column chooser.
    /// </summary>
    /// <remarks>
    /// The client draws the "manage columns" button in the header of the options column, and
    /// that column exists only while a row carries a menu - an empty result, or a table whose
    /// rows have none, could not have its columns chosen at all. The reorderable table the data
    /// table builds on shows the button regardless when its host says
    /// <c>data-allow-column-remove</c>; <see cref="ControlDataTable"/> has no property for it, so
    /// the attribute is written onto the host here.
    /// </remarks>
    /// <param name="id">The control id.</param>
    public class ColumnChoosingDataTable(string id = null) : ControlDataTable(id)
    {
        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var html = base.Render(renderContext, visualTree);
            var host = html as HtmlElement ?? (html as HtmlList)?.Elements.OfType<HtmlElement>().FirstOrDefault();

            host?.AddUserAttribute("data-allow-column-remove", "true");

            return html;
        }
    }
}
