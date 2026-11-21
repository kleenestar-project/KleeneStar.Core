using System;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A hidden form input whose value is computed per request.
    /// </summary>
    /// <remarks>
    /// A plain hidden input takes its value from the form context, which the form that renders
    /// it has to fill. A page of a data wizard renders its items in a context of its own that
    /// nothing outside can reach, so a value that follows from the request - the document the
    /// wizard was opened from, say - is supplied here instead and put into that context right
    /// before the input renders.
    /// </remarks>
    public class PresetHiddenInput : ControlFormItemInputHidden
    {
        /// <summary>
        /// Gets or sets the value the input carries, computed from the render context.
        /// </summary>
        public Func<IRenderControlContext, string> Value { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public PresetHiddenInput(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation carrying the computed value.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var value = Value?.Invoke(renderContext);

            if (value is not null)
            {
                renderContext?.SetValue(this, new ControlFormInputValueString(value));
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
