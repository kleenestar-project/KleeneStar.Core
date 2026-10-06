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

namespace KleeneStar.Core.WebFragment.Field
{
    /// <summary>
    /// Represents a clone form fragment for a field.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Field._fieldid_.Clone>]
    [Cache]
    public sealed class FieldCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the field.
        /// </summary>
        public ControlDataFormItemInputUnique FieldName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Name),
            Label = _ => "kleenestar.core:field.name.label",
            Placeholder = _ => "kleenestar.core:field.name.placeholder",
            Help = _ => "kleenestar.core:field.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Fields.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the field.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Field.Description),
            Label = _ => "kleenestar.core:field.description.label",
            Placeholder = _ => "kleenestar.core:field.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input text control for specifying the help text of the field.
        /// </summary>
        public ControlFormItemInputText HelpText { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.HelpText),
            Label = _ => "kleenestar.core:field.helptext.label",
            Placeholder = _ => "kleenestar.core:field.helptext.placeholder",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input text control for specifying the placeholder of the field.
        /// </summary>
        public ControlFormItemInputText FieldPlaceholder { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Placeholder),
            Label = _ => "kleenestar.core:field.placeholder.label",
            Placeholder = _ => "kleenestar.core:field.placeholder.placeholder",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the field type.
        /// </summary>
        public ControlDataFormItemInputSelection FieldTypeSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.FieldType),
            Label = _ => "kleenestar.core:field.fieldtype.label",
            Placeholder = _ => "kleenestar.core:field.fieldtype.placeholder",
            Help = _ => "kleenestar.core:field.fieldtype.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Fields.FieldType>().ToString())};

        /// <summary>
        /// Gets the input selection control for the cardinality.
        /// </summary>
        public ControlDataFormItemInputSelection CardinalitySelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Cardinality),
            Label = _ => "kleenestar.core:field.cardinality.label",
            Placeholder = _ => "kleenestar.core:field.cardinality.placeholder",
            Help = _ => "kleenestar.core:field.cardinality.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Fields.Cardinality>().ToString())};

        /// <summary>
        /// Gets the checkbox control for the required flag.
        /// </summary>
        public ControlFormItemInputCheck FieldRequired { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Required),
            Label = _ => "kleenestar.core:field.required.label",
            Help = _ => "kleenestar.core:field.required.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the checkbox control for the unique flag.
        /// </summary>
        public ControlFormItemInputCheck FieldUnique { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Unique),
            Label = _ => "kleenestar.core:field.unique.label",
            Help = _ => "kleenestar.core:field.unique.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the checkbox control for the deprecated flag.
        /// </summary>
        public ControlFormItemInputCheck FieldDeprecated { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Deprecated),
            Label = _ => "kleenestar.core:field.deprecated.label",
            Help = _ => "kleenestar.core:field.deprecated.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the input selection control for the access modifier.
        /// </summary>
        public ControlDataFormItemInputSelection AccessModifierSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.AccessModifier),
            Label = _ => "kleenestar.core:field.accessmodifier.label",
            Placeholder = _ => "kleenestar.core:field.accessmodifier.placeholder",
            Help = _ => "kleenestar.core:field.accessmodifier.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Fields.AccessModifier>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the default specification.
        /// </summary>
        public ControlFormItemInputText DefaultSpec { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.DefaultSpec),
            Label = _ => "kleenestar.core:field.defaultspec.label",
            Placeholder = _ => "kleenestar.core:field.defaultspec.placeholder",
            Help = _ => "kleenestar.core:field.defaultspec.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection FieldState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.State),
            Label = _ => "kleenestar.core:field.state.label",
            Placeholder = _ => "kleenestar.core:field.state.placeholder",
            Help = _ => "kleenestar.core:field.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Fields.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FieldCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(FieldName);
            Add(Description);
            Add(HelpText);
            Add(FieldPlaceholder);
            Add(FieldTypeSelection);
            Add(CardinalitySelection);
            Add(FieldRequired);
            Add(FieldUnique);
            Add(FieldDeprecated);
            Add(AccessModifierSelection);
            Add(DefaultSpec);
            Add(FieldState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Fields.Index>();
            ItemId = renderContext =>
            {
                var fieldId = renderContext.Request.GetParameter<FieldIdParameter>();
                return fieldId?.Value?.ToString();
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
