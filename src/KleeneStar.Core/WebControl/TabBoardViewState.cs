using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Binds the first service of a board view state to the address of its owning tab.
    /// </summary>
    public sealed class TabBoardViewState : ControlViewState<DataQueryState>
    {
        /// <summary>
        /// Initializes a board view state whose first service serves the board configuration.
        /// </summary>
        /// <param name="id">The view state element identifier.</param>
        public TabBoardViewState(string id) : base(id)
        {
        }

        /// <summary>
        /// Renders the view state with a tab binding applied before its services are initialized.
        /// </summary>
        /// <param name="renderContext">The current rendering context.</param>
        /// <param name="visualTree">The visual tree of the page.</param>
        /// <returns>The view state containing its tab-specific board service.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var html = base.Render(renderContext, visualTree);
            new Binding().Add(new BindTemplate().Add("boardservice", TypeBindMode.Attr,
                target: "wx-service:first-of-type", name: "base-uri")).ApplyUserAttributes(html);
            return html;
        }
    }
}
