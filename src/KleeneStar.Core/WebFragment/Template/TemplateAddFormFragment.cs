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

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents an add form fragment for a template.
    /// </summary>
    [Title("kleenestar.core:template.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Add>]
    [Cache]
    public sealed class TemplateAddFormFragment : FragmentControlDataFormAdd
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
        /// Gets the configuration for the class selection input used in the form.
        /// </summary>
        public ControlDataFormItemInputSelection ClassSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.ClassId),
            Label = _ => "kleenestar.core:template.class.label",
            Placeholder = _ => "kleenestar.core:template.class.placeholder",
            Help = _ => "kleenestar.core:template.class.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Index>().ToString())};

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
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_.Selection>().ToString())};


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(TemplateName);
            Add(Description);
            Add(Category);
            Add(ClassSelection);
            Add(ParentSelection);
            Add(TemplateState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Templates.Index>();
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
