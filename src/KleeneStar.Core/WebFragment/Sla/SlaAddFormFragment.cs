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

namespace KleeneStar.Core.WebFragment.Sla
{
    /// <summary>
    /// Add-form fragment for a new SLA policy. The form posts to the SLA REST CRUD endpoint
    /// which delegates to <see cref="KleeneStar.Core.WebManager.SlaManager.Add"/>.
    /// </summary>
    [Title("kleenestar.core:sla.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Slas._classid_.Add>]
    [Cache]
    public sealed class SlaAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the input control for the policy name (uniqueness validated server-side).
        /// </summary>
        public ControlDataFormItemInputUnique SlaName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.Name),
            Label = _ => "kleenestar.core:sla.name.label",
            Placeholder = _ => "kleenestar.core:sla.name.placeholder",
            Help = _ => "kleenestar.core:sla.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas.UniqueName>().ToString())
        };

        /// <summary>
        /// Gets the textarea control for the description (WYSIWYG).
        /// </summary>
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.Description),
            Label = _ => "kleenestar.core:sla.description.label",
            Placeholder = _ => "kleenestar.core:sla.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the selection control for the lifecycle state.
        /// </summary>
        public ControlDataFormItemInputSelection SlaState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.State),
            Label = _ => "kleenestar.core:sla.state.label",
            Placeholder = _ => "kleenestar.core:sla.state.placeholder",
            Help = _ => "kleenestar.core:sla.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas.State>().ToString())
        };

        /// <summary>
        /// Gets the selection control for the severity priority.
        /// </summary>
        public ControlDataFormItemInputSelection SlaPriority { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.Priority),
            Label = _ => "kleenestar.core:sla.priority.label",
            Placeholder = _ => "kleenestar.core:sla.priority.placeholder",
            Help = _ => "kleenestar.core:sla.priority.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas.Priority>().ToString())
        };

        /// <summary>
        /// Gets the selection control for the working-hours calendar. The endpoint is
        /// class-scoped so the dropdown only lists calendars belonging to the active class.
        /// </summary>
        public ControlDataFormItemInputSelection SlaCalendar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.CalendarId),
            Label = _ => "kleenestar.core:sla.calendar.label",
            Placeholder = _ => "kleenestar.core:sla.calendar.placeholder",
            Help = _ => "kleenestar.core:sla.calendar.help",
            StickySelection = _ => true,
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas._classid_.Calendar>()
                    .BindParameters(renderContext.Request)
                    .ToString()
            )
        };

        /// <summary>
        /// Gets the input control for the comma-separated pause-on statuses.
        /// </summary>
        public ControlFormItemInputText PauseOn { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.PauseOn),
            Label = _ => "kleenestar.core:sla.pauseon.label",
            Placeholder = _ => "kleenestar.core:sla.pauseon.placeholder",
            Help = _ => "kleenestar.core:sla.pauseon.help",
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public SlaAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(SlaName);
            Add(Description);
            Add(SlaState);
            Add(SlaPriority);
            Add(SlaCalendar);
            Add(PauseOn);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Slas.Index>();
        }

        /// <summary>
        /// Renders the form control as HTML.
        /// </summary>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
