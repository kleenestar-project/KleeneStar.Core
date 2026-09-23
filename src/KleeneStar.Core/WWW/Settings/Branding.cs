using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings
{
    /// <summary>
    /// Represents the application settings page, on which the title and the icon the
    /// installation is presented under are set.
    /// </summary>
    /// <remarks>
    /// The page itself only carries the explanation. The form that edits the identity is
    /// contributed by <see cref="WebFragment.Branding.BrandingEditFormFragment"/>, following the
    /// way the other settings pages are composed.
    /// </remarks>
    [Title("kleenestar.core:setting.branding.title")]
    [WebIcon<IconWindowMaximize>]
    [SettingGroup<SettingGroupGeneralGeneral>()]
    [SettingSection(SettingSection.Primary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class Branding : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Branding()
        {
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
                    "kleenestar.core:setting.branding.header"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.branding.description"
                ),
                Format = _ => TypeFormatText.Paragraph,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
