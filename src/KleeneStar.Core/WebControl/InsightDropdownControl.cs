using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents a dropdown control for selecting an insight.
    /// </summary>
    public class InsightDropdownControl : ControlDataDropdown
    {
        /// <summary>
        /// Gets the control link for adding a new insight.
        /// </summary>
        public ControlDropdownItemLink AddInsight { get; } = new()
        {
            Text = _ => "kleenestar.core:insight.add.label",
            Icon = _ => new IconPlus(),
            PrimaryAction = _ => new ActionModal("modal-form", CoreHub.GetUri<global::KleeneStar.Core.WWW.Insights.Add>(), TypeModalSize.ExtraLarge),
        };

        /// <summary>
        /// Gets the control link for managing insights.
        /// </summary>
        public ControlDropdownItemLink ManageInsight { get; } = new()
        {
            Text = _ => "kleenestar.core:insight.manage.label",
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Insights.Index>(),
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the dropdown control.</param>
        public InsightDropdownControl(string id)
            : base(id)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights.Dropdown>().ToString());

            Add(AddInsight);
            Add(ManageInsight);
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
