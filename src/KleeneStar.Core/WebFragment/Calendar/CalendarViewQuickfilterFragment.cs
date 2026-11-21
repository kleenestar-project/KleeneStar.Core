using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Calendar
{
    /// <summary>
    /// Quickfilter chips ("Active", "Archived", "Default") for the calendar view.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<CalendarViewFragment>]
    [Cache]
    public sealed class CalendarViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Stable HTML content id.
        /// </summary>
        public static readonly string ContentId = "id_6F9DA3F9E4E45F3BC3F9CF9D7AD5EEC3";

        /// <summary>
        /// Gets the REST-backed quickfilter control.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Calendars._classid_.Quickfilter>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public CalendarViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Quickfilter);
        }

        /// <summary>
        /// Converts the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
