using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Represents an add form fragment for an insight.
    /// </summary>
    [Title("kleenestar.core:insight.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Insights.Add>]
    [Cache]
    public sealed class InsightAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the insight.
        /// </summary>
        public ControlDataFormItemInputUnique InsightName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Insight.Name),
            Label = _ => "kleenestar.core:insight.name.label",
            Placeholder = _ => "kleenestar.core:insight.name.placeholder",
            Help = _ => "kleenestar.core:insight.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights.UniqueName>().ToString())};

        /// <summary>
        /// Gets the picker of the insight type - what kind of view the insight is. It stands on
        /// this dialog alone: the type is chosen once and kept by every edit and clone.
        /// </summary>
        public InsightTypeSelectionControl InsightType { get; } = new();

        /// <summary>
        /// Gets the input tag definition for the category field.
        /// </summary>
        public ControlFormItemInputTag Category { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Insight.Categories),
            Label = _ => "kleenestar.core:insight.category.label",
            Placeholder = _ => "kleenestar.core:insight.category.placeholder",
            Help = _ => "kleenestar.core:insight.category.help"
        };

        /// <summary>
        /// Gets the input text control for specifying the description of the insight.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Insight.Description),
            Label = _ => "kleenestar.core:insight.description.label",
            Placeholder = _ => "kleenestar.core:insight.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection InsightState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Insight.State),
            Label = _ => "kleenestar.core:insight.state.label",
            Placeholder = _ => "kleenestar.core:insight.state.placeholder",
            Help = _ => "kleenestar.core:insight.state.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightName);
            Add(InsightType);
            Add(Category);
            Add(Description);
            Add(InsightState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Insights.Index>();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
