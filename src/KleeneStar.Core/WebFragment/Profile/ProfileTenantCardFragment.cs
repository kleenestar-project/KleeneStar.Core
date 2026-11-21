using KleeneStar.Core.WebManager;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The card at the top of the "tenant and role" page naming the tenant the calling identity
    /// is currently working in, together with its size and the role held in it.
    /// </summary>
    /// <remarks>
    /// The tenant is not switched from here. Which tenant an account works in follows from the
    /// workspace it opened, so the switch lives in the workspace menu and this card only states
    /// where the settings below apply.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tenant>]
    [Order(0)]
    public sealed class ProfileTenantCardFragment : FragmentControlPanel
    {
        private readonly IIdentityManager _identityManager;
        private readonly ITenantManager _tenantManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="identityManager">
        /// The manager used to resolve the calling identity. Cannot be null.
        /// </param>
        /// <param name="tenantManager">
        /// The manager used to resolve the tenant the identity belongs to. Cannot be null.
        /// </param>
        public ProfileTenantCardFragment
        (
            IFragmentContext fragmentContext,
            IIdentityManager identityManager,
            ITenantManager tenantManager
        )
            : base(fragmentContext)
        {
            _identityManager = identityManager;
            _tenantManager = tenantManager;
        }

        /// <summary>
        /// Renders the tenant card. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or no identity can be resolved for the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var identity = _identityManager.GetCurrentIdentity(renderContext?.Request);

            if (identity is null)
            {
                return null;
            }

            var card = new ControlCard("profile-tenant-card")
            {
                Header = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tenant.active.label"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two)
            };

            card.Add(new ControlText()
            {
                Text = _ => DescribeTenant(identity, renderContext),
                Format = _ => TypeFormatText.Bold
            });

            card.Add(new ControlText()
            {
                Text = _ => DescribeMembership(identity, renderContext),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            card.Add(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tenant.active.help"),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            return card.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns the name of the tenant the identity works in, or the note that the account
        /// belongs to the operator side and is not a member of any tenant.
        /// </summary>
        /// <param name="identity">The calling identity.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The line naming the tenant.</returns>
        private string DescribeTenant(Model.Entities.Identity identity, IRenderControlContext renderContext)
        {
            if (!identity.TenantId.HasValue)
            {
                return I18N.Translate(renderContext, "kleenestar.core:profile.tenant.none");
            }

            var tenant = _tenantManager.GetTenant(identity.TenantId.Value);

            return tenant?.Name ?? I18N.Translate(renderContext, "kleenestar.core:profile.tenant.none");
        }

        /// <summary>
        /// Returns the line beneath the tenant name: the number of members and, when one is
        /// assigned, the role the identity holds together with the month it took effect.
        /// </summary>
        /// <param name="identity">The calling identity.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The membership line.</returns>
        private string DescribeMembership(Model.Entities.Identity identity, IRenderControlContext renderContext)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (identity.TenantId.HasValue)
            {
                var members = _identityManager
                    .GetIdentities(new WebExpress.WebIndex.Queries.Query<Model.Entities.Identity>())
                    .Count(x => x.TenantId == identity.TenantId);

                parts.Add(string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tenant.members"),
                    members
                ));
            }

            if (!string.IsNullOrWhiteSpace(identity.Role))
            {
                parts.Add(identity.Role);
            }

            if (identity.RoleSince.HasValue)
            {
                parts.Add(string.Format
                (
                    CultureInfo.CurrentCulture,
                    I18N.Translate(renderContext, "kleenestar.core:profile.tenant.rolesince"),
                    identity.RoleSince.Value.ToString("MM/yyyy", CultureInfo.CurrentCulture)
                ));
            }

            return string.Join(" · ", parts);
        }
    }
}
