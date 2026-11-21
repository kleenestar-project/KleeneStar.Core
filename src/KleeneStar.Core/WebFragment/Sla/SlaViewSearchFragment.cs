using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Sla
{
    /// <summary>
    /// Advanced-search input for the SLA-policy view, backed by the SLA WQL endpoint.
    /// </summary>
    [Section<SectionViewHeaderPrimary>]
    [Scope<SlaViewFragment>]
    [Cache]
    public sealed class SlaViewSearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Stable HTML content id used by the table to bind the search expression.
        /// </summary>
        public static readonly string ContentId = "id_3FAA1F60D9F1480A8E72FF7C9DC9E6DA";

        /// <summary>
        /// Gets the advanced-search control.
        /// </summary>
        public ControlAdvancedSearch Search { get; } = new(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas.Wql>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public SlaViewSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Search);
        }

        /// <summary>
        /// Renders the fragment.
        /// </summary>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
