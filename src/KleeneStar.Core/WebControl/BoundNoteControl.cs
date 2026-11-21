using System;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A note whose text is not known when the page is rendered, but is projected into
    /// it by another control on the same form.
    /// </summary>
    /// <remarks>
    /// The note names a binding key; a tile card carrying <c>data-wx-bind-{key}</c>
    /// writes its value into the note when it is selected, and the note stays hidden
    /// while no value has arrived. The object wizard uses it to state what the chosen
    /// template will add to the object beyond the fields the form itself asks for.
    /// </remarks>
    public class BoundNoteControl : Control
    {
        /// <summary>
        /// Gets or sets the binding key the note reads its text from.
        /// </summary>
        public Func<IRenderControlContext, string> Binding { get; set; }

        /// <summary>
        /// Gets or sets the icon shown in front of the text.
        /// </summary>
        public Func<IRenderControlContext, IIcon> Icon { get; set; } = _ => new IconInfo();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public BoundNoteControl(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var binding = Binding?.Invoke(renderContext);
            var icon = Icon?.Invoke(renderContext) as Icon;

            var html = new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("alert", "alert-info", "wx-bound-note", GetClasses(renderContext)),
                Style = "display: none"
            }
                .AddUserAttribute("data-wx-bind-visible", binding);

            if (icon is not null)
            {
                html.Add(new HtmlElementTextSemanticsI() { Class = Css.Concatenate(icon.Class, "me-2") });
            }

            html.Add(new HtmlElementTextSemanticsSpan().AddUserAttribute("data-wx-bind-text", binding));

            return html;
        }
    }
}
