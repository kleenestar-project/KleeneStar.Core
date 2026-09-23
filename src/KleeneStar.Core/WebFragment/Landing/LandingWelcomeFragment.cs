using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebIdentity;
using System.Net;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The start page as a visitor who is not signed in sees it: the installation's greeting and
    /// the sign-in, nothing else.
    /// </summary>
    /// <remarks>
    /// The signed-in start page describes the organization - its figures, its news, its
    /// activity - and none of that is for somebody the installation does not know yet; its
    /// fragments are gated on <see cref="SignedInCondition"/> and this one on the complement,
    /// so a page shows exactly one of the two. The greeting is the
    /// <see cref="Model.Entities.Branding.WelcomeText"/> an administrator writes on the branding
    /// page, rendered through <see cref="ProseText.ToSafeHtml"/> because the reader is anybody;
    /// without one a built-in sentence stands in. The sign-in is the framework's own login
    /// control on the session endpoint, and reloads the page into the signed-in one.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<SignedOutCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(5)]
    [Cache]
    public sealed class LandingWelcomeFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public LandingWelcomeFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the greeting beside the sign-in.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var greeting = new ControlHtml("landing-welcome-text")
            {
                Html = _ => Greeting(renderContext)
            };

            var login = new ControlDataLogin("landing-welcome-login")
                .DataService<global::KleeneStar.Core.WWW.Api._1_.Session>();

            var text = new HtmlElementTextContentDiv(greeting.Render(renderContext, visualTree))
            {
                Class = "ks-welcome-text"
            };

            var form = new HtmlElementTextContentDiv(login.Render(renderContext, visualTree))
            {
                Class = "ks-welcome-login"
            };

            return new HtmlElementTextContentDiv(text, form)
            {
                Id = "landing-welcome",
                Class = "ks-welcome"
            };
        }

        /// <summary>
        /// Returns the greeting: the one an administrator wrote, else the built-in one naming
        /// the installation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The markup.</returns>
        private static string Greeting(IRenderControlContext renderContext)
        {
            var branding = CoreHub.BrandingManager?.GetBranding();
            var written = branding?.WelcomeText;

            if (!ProseText.IsEmpty(written))
            {
                return ProseText.ToSafeHtml(written);
            }

            var title = string.IsNullOrWhiteSpace(branding?.Title)
                ? I18N.Translate(renderContext, "kleenestar.core:app.name")
                : branding.Title;

            return $"<h2>{WebUtility.HtmlEncode(I18N.Translate(renderContext.Request, "kleenestar.core:landing.welcome.title", title))}</h2>"
                + $"<p>{WebUtility.HtmlEncode(I18N.Translate(renderContext, "kleenestar.core:landing.welcome.text"))}</p>";
        }
    }
}
