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

namespace KleeneStar.Core.WebFragment.Priority
{
    /// <summary>
    /// Represents a clone form fragment for a priority.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Priority._priorityid_.Clone>]
    [Cache]
    public sealed class PriorityCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the priority.
        /// </summary>
        public ControlDataFormItemInputUnique PriorityName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Priority.Name),
            Label = _ => "kleenestar.core:priority.name.label",
            Placeholder = _ => "kleenestar.core:priority.name.placeholder",
            Help = _ => "kleenestar.core:priority.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Priorities.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the priority.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Priority.Description),
            Label = _ => "kleenestar.core:priority.description.label",
            Placeholder = _ => "kleenestar.core:priority.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection PriorityState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Priority.State),
            Label = _ => "kleenestar.core:priority.state.label",
            Placeholder = _ => "kleenestar.core:priority.state.placeholder",
            Help = _ => "kleenestar.core:priority.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Priorities.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public PriorityCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(PriorityName);
            Add(Description);
            Add(PriorityState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Priorities.Index>();
            ItemId = renderContext =>
            {
                var priorityId = renderContext.Request.GetParameter<PriorityIdParameter>();
                return priorityId?.Value?.ToString();
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
