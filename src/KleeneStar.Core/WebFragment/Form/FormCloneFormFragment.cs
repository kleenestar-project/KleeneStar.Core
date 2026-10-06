using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
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

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Represents a clone form fragment for a form.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Clone>]
    [Cache]
    public sealed class FormCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the form.
        /// </summary>
        public ControlDataFormItemInputUnique FormName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Form.Name),
            Label = _ => "kleenestar.core:form.name.label",
            Placeholder = _ => "kleenestar.core:form.name.placeholder",
            Help = _ => "kleenestar.core:form.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Forms.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the form.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Form.Description),
            Label = _ => "kleenestar.core:form.description.label",
            Placeholder = _ => "kleenestar.core:form.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection FormState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Form.State),
            Label = _ => "kleenestar.core:form.state.label",
            Placeholder = _ => "kleenestar.core:form.state.placeholder",
            Help = _ => "kleenestar.core:form.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Forms.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FormCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(FormName);
            Add(Description);
            Add(FormState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Forms.Index>();
            ItemId = renderContext =>
            {
                var formId = renderContext.Request.GetParameter<FormIdParameter>();
                return formId?.Value?.ToString();
            };
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
