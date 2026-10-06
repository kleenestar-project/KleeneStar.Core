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

namespace KleeneStar.Core.WebFragment.Calendar
{
    /// <summary>
    /// Add-form fragment for a new calendar. Persists via the calendar REST CRUD endpoint.
    /// </summary>
    [Title("kleenestar.core:calendar.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Calendars._classid_.Add>]
    [Cache]
    public sealed class CalendarAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the unique-name input control.
        /// </summary>
        public ControlDataFormItemInputUnique CalendarName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.Name),
            Label = _ => "kleenestar.core:calendar.name.label",
            Placeholder = _ => "kleenestar.core:calendar.name.placeholder",
            Help = _ => "kleenestar.core:calendar.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Calendars.UniqueName>().ToString())};

        /// <summary>
        /// Gets the description input control.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.Description),
            Label = _ => "kleenestar.core:calendar.description.label",
            Placeholder = _ => "kleenestar.core:calendar.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the timezone selection control.
        /// </summary>
        public ControlDataFormItemInputSelection CalendarTimeZone { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.TimeZone),
            Label = _ => "kleenestar.core:calendar.timezone.label",
            Placeholder = _ => "kleenestar.core:calendar.timezone.placeholder",
            Help = _ => "kleenestar.core:calendar.timezone.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Calendars.TimeZone>().ToString())};

        /// <summary>
        /// Gets the region input control.
        /// </summary>
        public ControlFormItemInputText CalendarRegion { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.Region),
            Label = _ => "kleenestar.core:calendar.region.label",
            Placeholder = _ => "kleenestar.core:calendar.region.placeholder",
            Help = _ => "kleenestar.core:calendar.region.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the state selection control.
        /// </summary>
        public ControlDataFormItemInputSelection CalendarState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.State),
            Label = _ => "kleenestar.core:calendar.state.label",
            Placeholder = _ => "kleenestar.core:calendar.state.placeholder",
            Help = _ => "kleenestar.core:calendar.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Calendars.State>().ToString())};

        /// <summary>
        /// Gets the "default" toggle.
        /// </summary>
        public ControlFormItemInputCheck IsDefault { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Calendar.IsDefault),
            Label = _ => "kleenestar.core:calendar.isdefault.label",
            Help = _ => "kleenestar.core:calendar.isdefault.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public CalendarAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(CalendarName);
            Add(Description);
            Add(CalendarTimeZone);
            Add(CalendarRegion);
            Add(CalendarState);
            Add(IsDefault);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Calendars.Index>();
        }

        /// <summary>
        /// Converts the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
