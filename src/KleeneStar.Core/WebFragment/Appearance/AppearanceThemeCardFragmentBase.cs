using System;
using System.Linq;
using WebExpress.WebApp.WebTheme;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Appearance
{
    /// <summary>
    /// The base of the theme card of an appearance page (core profile, portal profile): which theme the pages are drawn in, and whether
    /// light or dark.
    /// </summary>
    /// <remarks>
    /// Both are the framework's own selectors, created by <c>ThemePreferences</c> from what
    /// <see cref="WebTheme.AppearanceRegistration"/> registered for the application - the same two the system's theme page
    /// shows - so a pick here and a pick there are one preference. Each stores at once; the
    /// theme selector reloads the page because a theme is a stylesheet the server links, the
    /// mode switches in place. The themes offered are every theme WebExpress registered for
    /// the application: the core's default and those of a plugin such as
    /// <c>KleeneStar.Theme</c>. A caller who is not signed in has nowhere to keep a choice and
    /// gets nothing.
    /// </remarks>
    public abstract class AppearanceThemeCardFragmentBase : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected AppearanceThemeCardFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the two selectors under their captions.
        /// </summary>
        /// <remarks>
        /// The selectors are created per request: they are data islands that carry the
        /// caller's current value, and the fragment is cached and shared.
        /// </remarks>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree of the page.</param>
        /// <returns>The card, or <see langword="null"/> for a caller who is not signed in.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Check(renderContext?.Request)
                || CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request) == Guid.Empty)
            {
                return null;
            }

            var application = renderContext.PageContext?.ApplicationContext;
            var pickers = new (string Key, string Help, IControl Selector)[]
            {
                ("kleenestar.core:profile.appearance.theme.label", "kleenestar.core:profile.appearance.theme.help", ThemePreferences.CreateSelector(application)),
                ("kleenestar.core:profile.appearance.scheme.label", "kleenestar.core:profile.appearance.scheme.help", ThemePreferences.CreateColorSchemeSelector(application))
            }
                .Where(x => x.Selector is not null)
                .ToList();

            if (pickers.Count == 0)
            {
                return null;
            }

            // the framework's grid keeps captions and pickers in two aligned columns
            var choices = new ControlPanel()
            {
                Classes = ["wx-webapp-theme-choices"]
            };

            foreach (var (key, help, selector) in pickers)
            {
                choices.Add
                (
                    new ControlPanel().Add
                    (
                        // caption and help stack: the grid is the framework's, the help line
                        // belongs under its caption, and the card stands on pages of
                        // applications that do not load kleenestar.css (the portal)
                        new ControlText()
                        {
                            Text = _ => I18N.Translate(renderContext, key),
                            Format = _ => TypeFormatText.Bold,
                            Styles = ["display: block"]
                        },
                        new ControlText()
                        {
                            Text = _ => I18N.Translate(renderContext, help),
                            TextColor = _ => new PropertyColorText(TypeColorText.Muted),
                            Format = _ => TypeFormatText.Small,
                            Styles = ["display: block", "max-width: 28rem"]
                        }
                    ),
                    selector
                );
            }

            return new ControlPanel()
            {
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.Four)
            }
                .Add
                (
                    new ControlText()
                    {
                        Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.appearance.theme.section"),
                        Format = _ => TypeFormatText.H4
                    },
                    new ControlText()
                    {
                        Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.appearance.theme.description"),
                        Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two)
                    },
                    choices
                )
                .Render(renderContext, visualTree);
        }
    }
}
