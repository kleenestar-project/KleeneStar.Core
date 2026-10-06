using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebApp.WebData;
using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Status
{
    /// <summary>
    /// Represents a add form fragment for a state.
    /// </summary>
    [Title("kleenestar.core:status.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Statuses._classid_.Add>]
    [Cache]
    public sealed class StatusAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the hidden input carrying the class the status is created in.
        /// </summary>
        /// <remarks>
        /// The form posts to the collection endpoint, whose route names no class, so the
        /// class of the page the form was opened from has to travel in the payload — a
        /// status cannot be inserted without it.
        /// </remarks>
        public ControlFormItemInputHidden ClassId { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Status.ClassId)
        };

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
        public StatusAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(ClassId);
            Add(StateName);
            Add(Category);
            Add(Description);
            Add(StatusState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Statuses.Index>();
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
            // the page the form is shown on is class scoped, so the class it creates the
            // item in is taken from the request and carried in the hidden input
            var classId = renderContext?.Request?.GetParameter<ClassIdParameter>()?.Value;

            if (!string.IsNullOrWhiteSpace(classId))
            {
                renderContext.SetValue(ClassId, new ControlFormInputValueString(classId));
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
