using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The form on the "tenant and role" page: the business data an identity carries inside its
    /// tenant.
    /// </summary>
    /// <remarks>
    /// The role and the personnel number are shown but not editable. Both are assigned by the
    /// tenant, not chosen by its members — and the role in particular only labels what the
    /// group memberships already grant, so letting an account rewrite it would make the label
    /// lie about its own permissions.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tenant>]
    [Order(1)]
    [Cache]
    public sealed class ProfileTenantEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the read-only control showing the role inside the tenant.
        /// </summary>
        public ControlFormItemInputText TenantRole { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Role),
            Label = _ => "kleenestar.core:profile.tenant.role.label",
            Help = _ => "kleenestar.core:profile.tenant.role.help",
            Disabled = _ => true
        };

        /// <summary>
        /// Gets the input control for the department.
        /// </summary>
        public ControlFormItemInputText Department { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Department),
            Label = _ => "kleenestar.core:profile.tenant.department.label",
            Help = _ => "kleenestar.core:profile.tenant.department.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input control for the cost center.
        /// </summary>
        public ControlFormItemInputText CostCenter { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.CostCenter),
            Label = _ => "kleenestar.core:profile.tenant.costcenter.label",
            Help = _ => "kleenestar.core:profile.tenant.costcenter.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the read-only control showing the personnel number.
        /// </summary>
        public ControlFormItemInputText PersonnelNumber { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.PersonnelNumber),
            Label = _ => "kleenestar.core:profile.tenant.personnelnumber.label",
            Help = _ => "kleenestar.core:profile.tenant.personnelnumber.help",
            Disabled = _ => true
        };

        /// <summary>
        /// Gets the selection control for the deputy.
        /// </summary>
        public ControlDataFormItemInputSelection Deputy { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.DeputyId),
            Label = _ => "kleenestar.core:profile.tenant.deputy.label",
            Help = _ => "kleenestar.core:profile.tenant.deputy.help",
            Placeholder = _ => "kleenestar.core:profile.tenant.deputy.placeholder",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Profile.Deputy>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileTenantEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(TenantRole);
            Add(Department);
            Add(CostCenter);
            Add(PersonnelNumber);
            Add(Deputy);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Profile.Index>();

            ItemId = renderContext => CoreHub.SessionManager
                .GetCurrentIdentityId(renderContext?.Request)
                .ToString();
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
