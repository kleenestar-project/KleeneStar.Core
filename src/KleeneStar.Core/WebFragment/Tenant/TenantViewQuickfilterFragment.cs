using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

// the fragment exposes a Quickfilter property of its own, so the endpoint of the same name is
// reached through an alias rather than through a qualified name inside every reference
using TenantQuickfilterApi = KleeneStar.Core.WWW.Api._1_.Tenants.Quickfilter;

namespace KleeneStar.Core.WebFragment.Tenant
{
    /// <summary>
    /// Represents a fragment that provides a quick filter control for REST-based tenant queries in
    /// the tenant view.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<TenantViewFragment>]
    [Cache]
    public sealed class TenantViewQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content.
        /// </summary>
        public static readonly string ContentId = "id_5C8A91BD37624E2A8F4E0D0B72F61E83";

        /// <summary>
        /// Gets the quick filter control for REST-based tenant queries.
        /// </summary>
        public ControlDataQuickfilter Quickfilter { get; } = new ControlDataQuickfilter(ContentId)
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Tenants.Quickfilter>().ToString()),

            // the chips a user defined offer this from their own menu; the bar appends the filter
            // they stand for, so one dialog serves them all
            EditAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Edit>()
                    .BindParameters(renderContext.Request)
                    .Concat($"?view={TenantQuickfilterApi.ViewKey}"),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Gets the chip that opens the dialog in which a new quickfilter is defined.
        /// </summary>
        /// <remarks>
        /// The chip carries no filter and never shows active; the client keeps it at the trailing
        /// edge of the bar. It opens the bar's own editor, which writes through the quickfilter
        /// endpoint — that endpoint already knows which bar it serves, so nothing has to be carried
        /// along to say where the new filter belongs.
        /// </remarks>
        public ControlQuickfilterItemAdd AddFilter { get; } = new()
        {
            Tooltip = _ => "kleenestar.core:quickfilter.add.label",
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Quickfilters.Add>()
                    .BindParameters(renderContext.Request)
                    .Concat($"?view={TenantQuickfilterApi.ViewKey}"),
                TypeModalSize.Large
            )
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TenantViewQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Quickfilter.Add(AddFilter);

            Add(Quickfilter);
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
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
