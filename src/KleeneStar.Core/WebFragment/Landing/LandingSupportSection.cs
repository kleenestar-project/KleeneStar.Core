using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The help area: the first steps, the guides and the frequent questions an organization
    /// has marked with the reserved labels (<see cref="LandingLabel.FirstSteps"/>,
    /// <see cref="LandingLabel.Help"/>, <see cref="LandingLabel.Faq"/>), as one list.
    /// </summary>
    /// <remarks>
    /// It used to be three narrow columns across the wide one - a link list, an accordion with
    /// the first answer open and a vertical step control with its own create button - each
    /// empty column still explaining its label. Now it is one section in the side column: a
    /// caption per group that has entries, the entries as <see cref="LandingRow"/>s (a
    /// question leads to its page like a guide does), and a single sentence naming the labels
    /// when nothing is marked at all.
    /// </remarks>
    internal static class LandingSupportSection
    {
        /// <summary>
        /// The maximum number of entries per group.
        /// </summary>
        private const int MaxItems = 5;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="objectManager">The object manager used to resolve the labelled pages.</param>
        /// <returns>The section.</returns>
        public static IControl Build(IObjectTagManager tagManager, IObjectManager objectManager)
        {
            var groups = new (string Key, string Caption, IIcon Icon, IReadOnlyList<Model.Entities.Object> Pages)[]
            {
                ("firststeps", "kleenestar.core:landing.support.firststeps.label", new IconShoePrints(), LandingLabel.Resolve(tagManager, objectManager, LandingLabel.FirstSteps, MaxItems)),
                ("help", "kleenestar.core:landing.support.help.label", new IconFileLines(), LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Help, MaxItems)),
                ("faq", "kleenestar.core:landing.support.faq.label", new IconCircleQuestion(), LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Faq, MaxItems))
            };

            var section = new ControlSection("landing-support")
            {
                Header = _ => "kleenestar.core:landing.support.heading",
                HeaderIcon = _ => new IconLifeRing(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var any = false;

            foreach (var (key, caption, icon, pages) in groups)
            {
                if (pages.Count == 0)
                {
                    continue;
                }

                any = true;

                var list = LandingRow.List("landing-support-" + key);

                foreach (var page in pages)
                {
                    list.Add(LandingRow.Build
                    (
                        "landing-support-" + key + "-" + page.Id.ToString("N"),
                        icon,
                        page.Summary,
                        ObjectKindCatalog.ResolveDetailUri(page)
                    ));
                }

                section.Add(LandingRow.Caption(caption), list);
            }

            if (!any)
            {
                section.Add(LandingRow.Empty("landing-support-empty", "kleenestar.core:landing.support.empty"));
            }

            return section;
        }
    }
}
