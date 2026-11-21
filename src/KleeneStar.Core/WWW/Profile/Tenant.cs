using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Profile
{
    /// <summary>
    /// Tenant and role — the business data an identity carries inside the tenant it is
    /// currently working in: the tenant itself, the assigned role, department, cost center,
    /// personnel number and the deputy that takes over while the account is absent.
    /// </summary>
    /// <remarks>
    /// The page carries the explanation; the tenant card and the form are contributed by
    /// <see cref="WebFragment.Profile.ProfileTenantCardFragment"/> and
    /// <see cref="WebFragment.Profile.ProfileTenantEditFormFragment"/>.
    /// </remarks>
    [Title("kleenestar.core:profile.tenant.title")]
    [WebIcon<IconBuilding>]
    [Scope<IScopeGeneral>]
    public sealed class Tenant : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tenant()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:profile.tenant.title");

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.tenant.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
