using KleeneStar.Core.WebTheme;
using WebExpress.WebApp.WebTheme;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Appearance
{
    /// <summary>
    /// The base of the fragments (one per application: core and portal) that draw every page
    /// in the appearance its reader chose: switches the page to the picked
    /// theme and lays the adjustments (accent, font size, density, motion) over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WebExpress leaves a per-user theme to the application - "the page's <c>Process</c> calls
    /// <c>UseTheme</c>" - and only its own settings pages ask <c>ThemePreferences</c> by
    /// themselves. KleeneStar has hundreds of pages and no common base, so the one place every
    /// page passes is a fragment of <c>SectionBodyPreferences</c>: the visual tree renders
    /// it with itself as the argument, before the head is written out, and the head's style
    /// links are read lazily, so the theme swapped here is the one the browser loads. On a
    /// settings page the framework has already applied the same theme, which makes the call
    /// here a no-op there.
    /// </para>
    /// <para>
    /// The adjustments are a <c>&lt;style&gt;</c> of their own rather than an entry of the
    /// head's styles: those are written before the stylesheet links, so the theme would win
    /// every token the adjustments redefine. Standing in the body, the block comes after
    /// every stylesheet of the head and wins by order (<see cref="AppearanceStyle"/>).
    /// </para>
    /// <para>
    /// The light or dark mode needs nothing here: the framework marks the root itself from the
    /// resolver registered through <see cref="AppearanceRegistration"/>.
    /// </para>
    /// </remarks>
    public abstract class AppearanceFragmentBase : FragmentControlPanel
    {
        /// <summary>
        /// The name the style block carries in its leading comment, so it can be found in a page.
        /// </summary>
        public const string StyleId = "ks-appearance";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected AppearanceFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Applies the reader's theme and returns the override stylesheet, or nothing.
        /// </summary>
        /// <remarks>
        /// Read per request: the fragment is cached and shared, so nothing about the reader may
        /// be stored on it.
        /// </remarks>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree of the page.</param>
        /// <returns>The style block, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (visualTree is VisualTreeControl tree)
            {
                tree.UseTheme(ThemePreferences.Resolve(renderContext));
            }

            var css = AppearanceStyle.Build(CoreHub.AppearanceManager.GetCurrent(renderContext?.Request));

            // the element writes no attributes, so the block names itself in a comment
            return css is null
                ? null
                : new HtmlElementMetadataStyle("/* " + StyleId + " */\n" + css);
        }
    }
}
