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
    /// Represents the maintenance settings page, on which the instruction text shown to every user
    /// as a toast is written and switched on or off.
    /// </summary>
    /// <remarks>
    /// The page itself only carries the explanation. The form that edits the notice is contributed
    /// by <see cref="WebFragment.Maintenance.MaintenanceEditFormFragment"/>, following the way the
    /// other settings pages are composed.
    /// </remarks>
    [Title("kleenestar.core:setting.maintenance.title")]
    [WebIcon<IconWrench>]
    [SettingGroup<SettingGroupGeneralGeneral>()]
    [SettingSection(SettingSection.Primary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class Maintenance : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Maintenance()
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
                    "kleenestar.core:setting.maintenance.header"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.maintenance.description"
                ),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
