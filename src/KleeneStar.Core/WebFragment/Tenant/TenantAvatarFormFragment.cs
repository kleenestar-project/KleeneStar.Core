using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Tenant
{
    /// <summary>
    /// Represents a avatar form fragment for a tenant.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Avatar>]
    [Cache]
    public sealed class TenantAvatarFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input avatar control for assigning the icon of the tenant.
        /// </summary>
        public ControlFormItemInputAvatar Avatar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Tenant.Icon),
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TenantAvatarFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Avatar);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Tenants.Index>();
            ItemId = renderContext =>
            {
                var tenantId = renderContext.Request.GetParameter<TenantIdParameter>();
                return tenantId?.Value?.ToString();
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
