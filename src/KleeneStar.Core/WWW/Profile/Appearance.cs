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
    /// Appearance preferences - theme, light or dark, accent color, font size, density and
    /// motion - each the caller's own.
    /// </summary>
    /// <remarks>
    /// The page carries the explanation; the two cards are fragments, the way the other profile
    /// pages are composed: <see cref="WebFragment.Profile.ProfileAppearanceThemeFragment"/>
    /// (theme and mode, stored at once) and <see cref="WebFragment.Profile.ProfileAppearanceFormFragment"/>
    /// (the adjustments laid over the theme). The themes on offer are whatever WebExpress
    /// registered for the application, so a plugin adds its own without this page changing.
    /// </remarks>
    [Title("kleenestar.core:profile.appearance.title")]
    [WebIcon<IconPalette>]
    [Scope<IScopeGeneral>]
    public sealed class Appearance : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Appearance()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:profile.appearance.title");

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.appearance.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
