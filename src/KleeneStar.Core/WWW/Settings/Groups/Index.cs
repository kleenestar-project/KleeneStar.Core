using KleeneStar.Core.WebSettingPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Groups
{
    /// <summary>
    /// Represents the group management settings page.
    /// </summary>
    [Title("kleenestar.core:setting.group.title")]
    [WebIcon<IconUsers>]
    [SettingGroup<SettingGroupIdentityGeneral>()]
    [SettingSection(SettingSection.Primary)]
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
                    "kleenestar.core:setting.group.header"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.group.description"
                ),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
