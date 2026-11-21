using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// A tile selection (<see cref="ControlFormItemInputTile"/>) rendered with a preset value.
    /// </summary>
    /// <remarks>
    /// <c>ControlDataForm</c> does not call <see cref="ControlFormItemInput{TValue}.Initialize(IRenderControlFormContext)"/>
    /// on its items, so the initialize hook of the input never runs inside a REST form. The value
    /// is written into the render context before the tile renders and reaches the client as
    /// <c>data-value</c>. A form load overwrites it with the loaded value when that names a tile;
    /// a loaded value naming no tile keeps the preset.
    /// </remarks>
    /// <param name="value">The preset value: a tile id, several separated by semicolons.</param>
    public sealed class PresetInputTile(string value) : ControlFormItemInputTile
    {
        /// <summary>
        /// Renders the tile selection with the preset value.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            renderContext.SetValue(this, new ControlFormInputValueStringList(value));

            return base.Render(renderContext, visualTree);
        }
    }
}
