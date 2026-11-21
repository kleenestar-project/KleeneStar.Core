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
    /// Quickfilter chips ("Active", "Draft", "Inactive", "Critical") for the SLA-policy view.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<SlaViewFragment>]
    [Cache]
    public sealed class SlaViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Stable HTML content id.
        /// </summary>
        public static readonly string ContentId = "id_E8732FCDFBA94A1D9F71B91F86B7F03A";

        /// <summary>
        /// Gets the REST-backed quickfilter control.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Slas._classid_.Quickfilter>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public SlaViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Quickfilter);
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
