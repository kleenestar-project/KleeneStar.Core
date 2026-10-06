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

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents an edit form fragment for a template.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Template._templateid_.Edit>]
    [Cache]
    public sealed class TemplateEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control configuration for the template name field.
        /// </summary>
        public ControlDataFormItemInputUnique TemplateName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.Name),
            Label = _ => "kleenestar.core:template.name.label",
            Placeholder = _ => "kleenestar.core:template.name.placeholder",
            Help = _ => "kleenestar.core:template.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input control for editing the template description.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.Description),
            Label = _ => "kleenestar.core:template.description.label",
            Placeholder = _ => "kleenestar.core:template.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input control for specifying the category of the template.
        /// </summary>
        public ControlFormItemInputText Category { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.Category),
            Label = _ => "kleenestar.core:template.category.label",
            Placeholder = _ => "kleenestar.core:template.category.placeholder",
            Help = _ => "kleenestar.core:template.category.help"
        };

        /// <summary>
        /// Gets the input selection configuration for the template state field.
        /// </summary>
        public ControlDataFormItemInputSelection TemplateState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.State),
            Label = _ => "kleenestar.core:template.state.label",
            Placeholder = _ => "kleenestar.core:template.state.placeholder",
            Help = _ => "kleenestar.core:template.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.State>().ToString())};


        /// <summary>
        /// Gets the input selection configuration for the parent template.
        /// </summary>
        public ControlDataFormItemInputSelection ParentSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.ParentId),
            Label = _ => "kleenestar.core:template.parent.label",
            Placeholder = _ => "kleenestar.core:template.parent.placeholder",
            Help = _ => "kleenestar.core:template.parent.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates._templateid_.Candidates>().ToString())};


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(TemplateName);
            Add(Description);
            Add(Category);
            Add(ParentSelection);
            Add(TemplateState);

            // the class a template instantiates is chosen once, when the template is created:
            // re-pointing an existing template at another class would leave its presets
            // referring to fields that class does not have, so the class is not editable here
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Templates.Index>();
            ItemId = renderContext =>
            {
                var templateId = renderContext.Request.GetParameter<TemplateIdParameter>();
                return templateId?.Value?.ToString();
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
