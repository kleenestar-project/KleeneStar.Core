using KleeneStar.Core.WebManager;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The head of the landing page: the date line, the greeting, the sentence saying what the
    /// page is, and the two actions that belong to arriving - choosing a different start page
    /// and raising an issue.
    /// </summary>
    /// <remarks>
    /// The greeting addresses the reader by their first name and follows the time of day, so
    /// the page reads as somebody's morning rather than as a report. It is not decoration: on
    /// a page everybody shares, the one personal line is what tells a reader the figures below
    /// are the organization's and not theirs.
    /// <para>
    /// The head is rendered here rather than through the page headline because it carries a
    /// kicker above the title and actions beside it, which the headline control does not
    /// express. The page therefore leaves <c>Headline.Title</c> unset and the framework hides
    /// the empty header.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<global::KleeneStar.Core.WWW.Index>]
    [Order(10)]
    public sealed class LandingHeadFragment : FragmentControlPanel
    {
        private readonly IIdentityManager _identityManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="identityManager">The identity manager used to greet the reader by name.</param>
        public LandingHeadFragment(IFragmentContext fragmentContext, IIdentityManager identityManager)
            : base(fragmentContext)
        {
            _identityManager = identityManager;
        }

        /// <summary>
        /// Renders the head. Returns <c>null</c> when the fragment's render conditions exclude it.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var culture = LandingHtml.Culture(renderContext);
            var now = DateTime.Now;

            var kicker = LandingHtml.Join
            (
                I18N.Translate(renderContext, "kleenestar.core:landing.kicker"),
                now.ToString("D", culture)
            );

            var head = new HtmlElementTextContentDiv
            (
                new HtmlElementTextContentDiv(new HtmlText(kicker)) { Class = "ks-landing-kicker" },
                BuildRow(renderContext, visualTree)
            )
            {
                Id = "landing-head",
                Class = "ks-landing-head"
            };

            return head;
        }

        /// <summary>
        /// Builds the row carrying the greeting and the lede on the left and the actions on
        /// the right.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The row element.</returns>
        private IHtmlNode BuildRow(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var text = new HtmlElementTextContentDiv
            (
                new HtmlElementSectionH1(new HtmlText(BuildGreeting(renderContext))) { Class = "ks-landing-greeting" },
                new HtmlElementTextContentP(new HtmlText(I18N.Translate(renderContext, "kleenestar.core:landing.description")))
                {
                    Class = "ks-landing-lede"
                }
            );

            return new HtmlElementTextContentDiv(text, BuildActions(renderContext, visualTree))
            {
                Class = "ks-landing-head-row"
            };
        }

        /// <summary>
        /// Builds the greeting: the phrase for the time of day, addressed to the reader's
        /// first name when one is known.
        /// </summary>
        /// <remarks>
        /// Composed here rather than in the resource file because the name is data: the
        /// pattern carries the placeholder and is translated first, so the sentence keeps the
        /// word order of its language.
        /// </remarks>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The greeting.</returns>
        private string BuildGreeting(IRenderControlContext renderContext)
        {
            var hour = DateTime.Now.Hour;

            var phrase = hour switch
            {
                < 5 => "kleenestar.core:landing.greeting.night",
                < 11 => "kleenestar.core:landing.greeting.morning",
                < 18 => "kleenestar.core:landing.greeting.day",
                _ => "kleenestar.core:landing.greeting.evening"
            };

            var identity = _identityManager?.GetCurrentIdentity(renderContext?.Request);
            var name = FirstName(identity?.Name);

            return string.IsNullOrWhiteSpace(name)
                ? I18N.Translate(renderContext, phrase + ".anonymous")
                : string.Format(LandingHtml.Culture(renderContext), I18N.Translate(renderContext, phrase), name);
        }

        /// <summary>
        /// Returns the first word of a name, which is the part a greeting uses.
        /// </summary>
        /// <param name="name">The full name.</param>
        /// <returns>The first name, or <c>null</c>.</returns>
        private static string FirstName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length == 0 ? null : parts[0];
        }

        /// <summary>
        /// Builds the two actions of the head: replacing the start page, and raising an issue.
        /// </summary>
        /// <remarks>
        /// "Choose start page" leads to the dashboards, because that is what a reader who has
        /// outgrown this page replaces it with. The landing page says so itself rather than
        /// leaving the reader to discover that it can be left behind.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The actions element.</returns>
        private static IHtmlNode BuildActions(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var choose = new ControlButtonLink("landing-choose-start")
            {
                Text = _ => "kleenestar.core:landing.action.choose",
                Icon = _ => new IconTableCells(),
                Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Dashboards.Index>(),
                Outline = _ => true,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary)
            };

            // the same modal the add button of every kind overview opens, so raising an issue
            // from here is the identical act and not a second, parallel path into creation
            var create = new ControlButton("landing-new-issue")
            {
                Text = _ => "kleenestar.core:landing.action.create",
                Icon = _ => new IconPlus(),
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Highlight),
                PrimaryAction = _ => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>(),
                    TypeModalSize.ExtraLarge
                )
            };

            return new HtmlElementTextContentDiv
            (
                LandingHtml.Render(choose, renderContext, visualTree),
                LandingHtml.Render(create, renderContext, visualTree)
            )
            {
                Class = "ks-landing-head-actions"
            };
        }
    }
}
