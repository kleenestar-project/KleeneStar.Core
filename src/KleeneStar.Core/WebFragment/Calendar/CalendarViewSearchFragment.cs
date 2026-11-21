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
    /// Advanced-search input for the calendar view, backed by the calendar WQL endpoint.
    /// </summary>
    [Section<SectionViewHeaderPrimary>]
    [Scope<CalendarViewFragment>]
    [Cache]
    public sealed class CalendarViewSearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Stable HTML content id used by the table to bind the search expression.
        /// </summary>
        public static readonly string ContentId = "id_4D7B91D7C2C24F19A1D7AE7B58F3CBF1";

        /// <summary>
        /// Gets the advanced-search control.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Calendars.Wql>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public CalendarViewSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Search);
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
