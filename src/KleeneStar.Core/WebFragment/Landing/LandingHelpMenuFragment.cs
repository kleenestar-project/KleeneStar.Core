using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// Fills the help menu in the application header with the pages the organization has
    /// labelled for help, frequently asked questions and first steps - the same pages the
    /// help area of the landing page lists.
    /// </summary>
    /// <remarks>
    /// The help of an installation is worth little if it can only be reached from the page
    /// somebody has just navigated away from, so the labelled pages are offered from every
    /// page as well. The menu is the second reader of the labels; it stays in step with the
    /// landing page because both resolve through <see cref="LandingLabel"/>.
    /// <para>
    /// The header slot takes one fragment of type
    /// <see cref="FragmentControlDropdownItemLink"/> but the number of labelled pages is
    /// only known at runtime, so the entries are returned as an <see cref="HtmlList"/> -
    /// it emits its children without a wrapper and they stay direct siblings of the
    /// surrounding dropdown list. The same trick the app-navigator fragment uses.
    /// </para>
    /// </remarks>
    [Section<SectionAppHelpPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class LandingHelpMenuFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// The maximum number of pages offered per label. The menu is a shortcut, not the
        /// index - the landing page carries the full listing.
        /// </summary>
        private const int MaxItemsPerLabel = 5;

        private readonly IObjectManager _objectManager;
        private readonly IObjectTagManager _tagManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        /// <param name="objectManager">The object manager used to resolve the labelled pages.</param>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        public LandingHelpMenuFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IObjectTagManager tagManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _tagManager = tagManager;
        }

        /// <summary>
        /// Renders one entry per labelled page. Returns <c>null</c> when the fragment's
        /// render conditions exclude it.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The entries, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var nodes = new HtmlList();

            AddEntries(nodes, LandingLabel.Help, new IconLifeRing(), renderContext, visualTree);
            AddEntries(nodes, LandingLabel.Faq, new IconCircleQuestion(), renderContext, visualTree);
            AddEntries(nodes, LandingLabel.FirstSteps, new IconShoePrints(), renderContext, visualTree);

            if (!nodes.Elements.Any())
            {
                // nothing labelled yet: point at the landing page, whose help area explains
                // how a page gets a label
                var fallback = new ControlDropdownItemLink(Id + "-none")
                {
                    Text = _ => "kleenestar.core:landing.support.heading",
                    Icon = _ => new IconLifeRing(),
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Index>()
                };

                nodes.Add(fallback.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Appends one entry per page carrying the supplied label.
        /// </summary>
        /// <param name="nodes">The list the entries are appended to.</param>
        /// <param name="label">The reserved label to read.</param>
        /// <param name="icon">The icon shared by the entries of this label.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        private void AddEntries
        (
            HtmlList nodes,
            string label,
            IIcon icon,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            IReadOnlyList<Model.Entities.Object> pages = LandingLabel
                .Resolve(_tagManager, _objectManager, label, MaxItemsPerLabel);

            foreach (var page in pages)
            {
                var entry = new ControlDropdownItemLink(Id + "-" + page.Id.ToString("N"))
                {
                    Text = _ => page.Summary,
                    Icon = _ => icon,
                    Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUri(page)
                };

                nodes.Add(entry.Render(renderContext, visualTree));
            }
        }
    }
}
