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

namespace KleeneStar.Core.WebFragment.Status
{
    /// <summary>
    /// Represents a clone form fragment for a state.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Status._statusid_.Clone>]
    [Cache]
    public sealed class StatusCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the state.
        /// </summary>
        public ControlDataFormItemInputUnique StateName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Status.Name),
            Label = _ => "kleenestar.core:status.name.label",
            Placeholder = _ => "kleenestar.core:status.name.placeholder",
            Help = _ => "kleenestar.core:status.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Statuses.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input selection control for the category status resource.
        /// </summary>
        public ControlDataFormItemInputSelection Category { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Status.Category),
            Label = _ => "kleenestar.core:status.category.label",
            Placeholder = _ => "kleenestar.core:status.category.placeholder",
            Help = _ => "kleenestar.core:status.category.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Statuses.Category>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the state.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Status.Description),
            Label = _ => "kleenestar.core:status.description.label",
            Placeholder = _ => "kleenestar.core:status.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection StatusState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Status.State),
            Label = _ => "kleenestar.core:status.state.label",
            Placeholder = _ => "kleenestar.core:status.state.placeholder",
            Help = _ => "kleenestar.core:status.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Statuses.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public StatusCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(StateName);
            Add(Category);
            Add(Description);
            Add(StatusState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Statuses.Index>();
            ItemId = renderContext =>
            {
                var stateId = renderContext.Request.GetParameter<WorkflowStateIdParameter>();
                return stateId?.Value?.ToString();
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
