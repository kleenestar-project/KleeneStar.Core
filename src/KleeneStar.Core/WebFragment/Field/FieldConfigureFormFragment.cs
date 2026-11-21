using KleeneStar.Core.WebParameter;
using System.Collections.Generic;
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
    /// Represents the configure form fragment for a field.
    /// Provides tabbed controls for Cardinality, Validation, Options, Filter objects,
    /// Workflow, and Priority configuration categories.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Field._fieldid_.Configure>]
    [Cache]
    public sealed class FieldConfigureFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the numeric input control for the minimum number of values the field must contain.
        /// </summary>
        public ControlFormItemInputText CardinalityMin { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.CardinalityMin),
            Label = _ => "kleenestar.core:field.configure.cardinality.min.label",
            Placeholder = _ => "kleenestar.core:field.configure.cardinality.min.placeholder",
            Help = _ => "kleenestar.core:field.configure.cardinality.min.help"
        };

        /// <summary>
        /// Gets the checkbox control that, when enabled, removes the upper bound on the field's value count.
        /// </summary>
        public ControlFormItemInputCheck CardinalityUnlimited { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.CardinalityUnlimited),
            Label = _ => "kleenestar.core:field.configure.cardinality.unlimited.label",
            Help = _ => "kleenestar.core:field.configure.cardinality.unlimited.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the numeric input control for the maximum number of values the field may contain.
        /// Ignored when <see cref="CardinalityUnlimited"/> is enabled.
        /// </summary>
        public ControlFormItemInputText CardinalityMax { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.CardinalityMax),
            Label = _ => "kleenestar.core:field.configure.cardinality.max.label",
            Placeholder = _ => "kleenestar.core:field.configure.cardinality.max.placeholder",
            Help = _ => "kleenestar.core:field.configure.cardinality.max.help"
        };

        /// <summary>
        /// Gets the text input control for specifying a regular expression pattern to validate field values.
        /// Applies to text and string field types.
        /// </summary>
        public ControlFormItemInputText RegexPattern { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.RegexPattern),
            Label = _ => "kleenestar.core:field.configure.validation.regex.label",
            Placeholder = _ => "kleenestar.core:field.configure.validation.regex.placeholder",
            Help = _ => "kleenestar.core:field.configure.validation.regex.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the tag input control for maintaining the list of selectable option values
        /// for enumerable field types such as Selection.
        /// </summary>
        public ControlFormItemInputTag Options { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.Options),
            Label = _ => "kleenestar.core:field.configure.options.label",
            Placeholder = _ => "kleenestar.core:field.configure.options.placeholder",
            Help = _ => "kleenestar.core:field.configure.options.help"
        };

        /// <summary>
        /// Gets the text input control for specifying a WQL filter expression that restricts
        /// permissible target objects for referential field types.
        /// </summary>
        public ControlFormItemInputText FilterWql { get; } = new()
        {
            Name = _ => "Wql",
            Label = _ => "kleenestar.core:field.configure.wql.label",
            Placeholder = _ => "kleenestar.core:field.configure.wql.placeholder",
            Help = _ => "kleenestar.core:field.configure.wql.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the selection control for assigning an active workflow to this field.
        /// Only active workflows compatible with the field's class are available.
        /// Applies to fields of type Workflow.
        /// </summary>
        public ControlDataFormItemInputSelection WorkflowSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.WorkflowId),
            Label = _ => "kleenestar.core:field.configure.workflow.label",
            Placeholder = _ => "kleenestar.core:field.configure.workflow.placeholder",
            Help = _ => "kleenestar.core:field.configure.workflow.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workflows.Index>().ToString())};

        /// <summary>
        /// Gets the selection control for choosing the default priority for this field.
        /// Applies to fields of type Priority (Reference with priority semantics).
        /// </summary>
        public ControlDataFormItemInputSelection DefaultPrioritySelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.DefaultPriorityId),
            Label = _ => "kleenestar.core:field.configure.priority.default.label",
            Placeholder = _ => "kleenestar.core:field.configure.priority.default.placeholder",
            Help = _ => "kleenestar.core:field.configure.priority.default.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Priorities.Index>().ToString())};

        /// <summary>
        /// Gets the dual-list transfer control for selecting the priorities available for this field.
        /// </summary>
        public ControlDataFormItemInputSelection SelectedPriorities { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Field.SelectedPriorityIds),
            Label = _ => "kleenestar.core:field.configure.priority.selected.label",
            Placeholder = _ => "kleenestar.core:field.configure.priority.available.label",
            Help = _ => "kleenestar.core:field.configure.priority.selected.label",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Priorities.Index>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FieldConfigureFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Fields.Index>();
            ItemId = renderContext =>
            {
                var fieldId = renderContext.Request.GetParameter<FieldIdParameter>();
                return fieldId?.Value?.ToString();
            };
            CardinalityMax.Bind = _ => new Binding().Add(new BindDisable()
            {
                Source = CardinalityUnlimited.Id,
                Condition = "true"
            });
        }

        /// <summary>
        /// Renders the control as an HTML node. Only the configuration categories that are
        /// meaningful for the field's <see cref="Model.Entities.FieldType"/> are emitted as
        /// tab views; non-applicable categories are hidden. When the field type resolves to a
        /// scalar value with nothing to configure (e.g. Boolean), an explanatory note is shown
        /// instead of an empty form.
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
            var param = renderContext.Request.GetParameter<FieldIdParameter>();
            var field = param is not null ? CoreHub.FieldManager.GetField(param) : null;

            // A null type means the field could not be resolved; the predicates below then
            // fall back to exposing every category rather than hiding the whole form.
            var type = field?.FieldType;

            var views = new List<IControlFormItemGroupTabView>();

            if (AppliesToCardinality(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.cardinality"
                }
                    .Add(CardinalityMin)
                    .Add(CardinalityUnlimited)
                    .Add(CardinalityMax));
            }

            if (AppliesToValidation(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.validation"
                }
                    .Add(RegexPattern));
            }

            if (AppliesToOptions(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.options"
                }
                    .Add(Options));
            }

            if (AppliesToFilter(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.filter"
                }
                    .Add(FilterWql));
            }

            if (AppliesToWorkflow(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.workflow"
                }
                    .Add(WorkflowSelection));
            }

            if (AppliesToPriority(type))
            {
                views.Add((IControlFormItemGroupTabView)new ControlFormItemGroupTabView()
                {
                    Title = _ => "kleenestar.core:field.configure.tab.priority"
                }
                    .Add(DefaultPrioritySelection)
                    .Add(SelectedPriorities));
            }

            if (views.Count == 0)
            {
                // No configuration category applies to this field type (e.g. Boolean).
                // Render an explanatory note rather than an empty form.
                var note = new ControlFormItemStaticText()
                {
                    Text = _ => "kleenestar.core:field.configure.empty"
                };

                return base.Render(renderContext, visualTree, [note]);
            }

            var tab = new ControlFormItemGroupTab();
            tab.AddView([.. views]);

            return base.Render(renderContext, visualTree, [tab]);
        }

        /// <summary>
        /// Determines whether the Cardinality category applies to the given field type.
        /// Cardinality controls value multiplicity, so it is hidden for strictly scalar
        /// field types (Text, Boolean, Workflow, Priority) that always hold a single value.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToCardinality(Model.Entities.FieldType? type)
            => type is not (Model.Entities.FieldType.Text
                or Model.Entities.FieldType.Multiline
                or Model.Entities.FieldType.RichText
                or Model.Entities.FieldType.Password
                or Model.Entities.FieldType.Boolean
                or Model.Entities.FieldType.Workflow
                or Model.Entities.FieldType.Priority
                or Model.Entities.FieldType.Color
                or Model.Entities.FieldType.Rating
                or Model.Entities.FieldType.Slider
                or Model.Entities.FieldType.Range
                or Model.Entities.FieldType.Estimate
                or Model.Entities.FieldType.TrafficLight
                or Model.Entities.FieldType.DateRange
                or Model.Entities.FieldType.CalendarRange
                or Model.Entities.FieldType.Avatar
                or Model.Entities.FieldType.Choice
                or Model.Entities.FieldType.Radio);

        /// <summary>
        /// Determines whether the Validation category applies to the given field type.
        /// Regular-expression validation is only meaningful for fields whose value is typed
        /// as free text.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToValidation(Model.Entities.FieldType? type)
            => type is null
                or Model.Entities.FieldType.Text
                or Model.Entities.FieldType.Multiline
                or Model.Entities.FieldType.Password;

        /// <summary>
        /// Determines whether the Options category applies to the given field type.
        /// Selectable option values are meaningful for every field the user picks from a
        /// list the field itself defines, whichever control that list is offered through.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToOptions(Model.Entities.FieldType? type)
            => type is null
                or Model.Entities.FieldType.Selection
                or Model.Entities.FieldType.MultiSelection
                or Model.Entities.FieldType.Choice
                or Model.Entities.FieldType.Radio
                or Model.Entities.FieldType.Tile
                or Model.Entities.FieldType.Move
                or Model.Entities.FieldType.Cascading;

        /// <summary>
        /// Determines whether the Filter objects category applies to the given field type.
        /// A WQL target filter is only meaningful for referential fields.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToFilter(Model.Entities.FieldType? type)
            => type is null or Model.Entities.FieldType.Reference;

        /// <summary>
        /// Determines whether the Workflow category applies to the given field type.
        /// Workflow assignment is only meaningful for fields of type Workflow.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToWorkflow(Model.Entities.FieldType? type)
            => type is null or Model.Entities.FieldType.Workflow;

        /// <summary>
        /// Determines whether the Priority category applies to the given field type.
        /// Priority assignment is only meaningful for fields of type Priority.
        /// </summary>
        /// <param name="type">The resolved field type, or <c>null</c> when it cannot be determined.</param>
        /// <returns><c>true</c> when the category should be shown; otherwise <c>false</c>.</returns>
        private static bool AppliesToPriority(Model.Entities.FieldType? type)
            => type is null or Model.Entities.FieldType.Priority;
    }
}

