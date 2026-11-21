using System;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The state selection of a dialog: required, without a way to clear it, and preset with a
    /// default state (usually the active one) while the form carries no value.
    /// </summary>
    /// <remarks>
    /// The default is written into the render context before the selection renders and reaches
    /// the client as <c>data-value</c>, as <see cref="PresetInputTile"/> does for tiles. An edit or
    /// clone dialog loads its record afterwards and overwrites it with the stored state; a create
    /// dialog loads no record, so the default stays.
    /// </remarks>
    public class StateInputSelection : ControlDataFormItemInputSelection
    {
        /// <summary>
        /// Gets or sets the id of the state the selection starts with, as the state endpoint
        /// names it.
        /// </summary>
        public Func<IRenderControlContext, string> DefaultValue { get; set; }

        /// <summary>
        /// Initializes a new instance of the class: required, and a chosen state cannot be cleared.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public StateInputSelection(string id = null)
            : base(id)
        {
            Required = _ => true;
            StickySelection = _ => true;
        }

        /// <summary>
        /// Renders the selection with the default state where the form carries none.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var current = renderContext?.GetValue<ControlFormInputValueString>(this)?.Text;
            var fallback = DefaultValue?.Invoke(renderContext);

            if (string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(fallback))
            {
                renderContext.SetValue(this, new ControlFormInputValueString(fallback));
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
