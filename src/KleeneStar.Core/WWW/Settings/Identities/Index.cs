using KleeneStar.Core.WebSettingPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Identities
{
    /// <summary>
    /// Represents the identity management settings page.
    /// </summary>
    [Title("kleenestar.core:setting.identity.title")]
    [WebIcon<IconUser>]
    [SettingGroup<SettingGroupIdentityGeneral>()]
    [SettingSection(SettingSection.Secondary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class Index : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebAppSetting visualTree)
        {
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.identity.header"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.identity.description"
                ),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
