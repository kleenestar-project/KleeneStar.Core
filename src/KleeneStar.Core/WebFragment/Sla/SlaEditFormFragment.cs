using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
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
    /// Edit-form fragment for an existing SLA policy. Re-uses the SLA REST CRUD endpoint
    /// for persistence.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Sla._slaid_.Edit>]
    [Cache]
    public sealed class SlaEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the policy name.
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
        /// class-scoped — the class id is resolved from the SLA id parameter carried by the
        /// edit page URL.
        /// </summary>
        public ControlDataFormItemInputSelection SlaCalendar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SlaPolicy.CalendarId),
            Label = _ => "kleenestar.core:sla.calendar.label",
            Placeholder = _ => "kleenestar.core:sla.calendar.placeholder",
            Help = _ => "kleenestar.core:sla.calendar.help",
            StickySelection = _ => true,
            ServiceFactory = renderContext =>
            {
                var slaParam = renderContext.Request.GetParameter<SlaIdParameter>();
                var slaId = Guid.TryParse(slaParam?.Value, out var id) ? id : Guid.Empty;
                var policy = CoreHub.SlaManager.GetSla(slaId);

                // bind the request FIRST, then bind the resolved class id LAST so that
                // the explicit class id always wins over an empty/missing one from the request.
                return DataServiceDescriptor.QueryData
                (
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas._classid_.Calendar>()
                        .BindParameters(renderContext.Request)
                        .BindParameters(new ClassIdParameter(policy?.ClassId ?? Guid.Empty))
                        .ToString()
                );
            }
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
        public SlaEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(SlaName);
            Add(Description);
            Add(SlaState);
            Add(SlaPriority);
            Add(SlaCalendar);
            Add(PauseOn);
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Slas.Index>();
            ItemId = renderContext =>
            {
                var slaId = renderContext.Request.GetParameter<SlaIdParameter>();
                return slaId?.Value;
            };
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
