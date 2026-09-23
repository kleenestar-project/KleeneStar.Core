using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebSettingPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Tenants
{
    /// <summary>
    /// Represents the tenant management settings page, providing an overview of workspace-tenant assignments.
    /// </summary>
    [Title("kleenestar.core:setting.tenant.title")]
    [WebIcon<IconBuilding>]
    [SettingGroup<SettingGroupIdentityGeneral>()]
    [SettingSection(SettingSection.Secondary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class Index : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="workspaceManager">
        /// The workspace manager used to retrieve workspace and tenant information. Cannot be null.
        /// </param>
        public Index(IWorkspaceManager workspaceManager)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebAppSetting visualTree)
        {
            // section header
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.tenant.header"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.tenant.description"
                ),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
