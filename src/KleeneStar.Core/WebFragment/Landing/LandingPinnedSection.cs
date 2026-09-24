using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The pinned area: the handful of objects the organization has promoted with the
    /// <see cref="LandingLabel.Pinned"/> label - the org chart, the central guidelines, the
    /// documents nobody should have to search for.
    /// </summary>
    /// <remarks>
    /// The area owns no content. What appears here is decided by the label on an object, so
    /// pinning is done where the object lives - its label line - rather than in a settings page
    /// for the landing page. Without any pinned object the area still renders its heading and
    /// says how something gets here: an empty area that explains itself is what a newcomer
    /// needs, an area that disappears teaches nothing.
    /// <para>
    /// An entry is a <see cref="LandingRow"/> - title, key and date - without its description:
    /// in the narrow column the full descriptions made every entry a column of text, and a
    /// pinned page is recognized by its title.
    /// </para>
    /// </remarks>
    internal static class LandingPinnedSection
    {
        /// <summary>
        /// The maximum number of pinned objects shown.
        /// </summary>
        private const int MaxItems = 8;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="objectManager">The object manager used to resolve the pinned objects.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The section.</returns>
        public static IControl Build(IObjectTagManager tagManager, IObjectManager objectManager, IRenderControlContext renderContext)
        {
            var pinned = LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Pinned, MaxItems);

            var section = new ControlSection("landing-pinned")
            {
                Header = _ => "kleenestar.core:landing.pinned.card",
                HeaderIcon = _ => new IconThumbtack(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (pinned.Count == 0)
            {
                section.Add(LandingRow.Empty("landing-pinned-empty", "kleenestar.core:landing.pinned.empty"));

                return section;
            }

            var culture = LandingHtml.Culture(renderContext);
            var list = LandingRow.List("landing-pinned-list");

            foreach (var entry in pinned)
            {
                var kind = ObjectKindCatalog.GetKind(entry.Kind);
                var updated = string.Format
                (
                    culture,
                    I18N.Translate(renderContext, "kleenestar.core:landing.pinned.updated"),
                    entry.Updated.ToString("d", culture)
                );

                list.Add(LandingRow.Build
                (
                    "landing-pinned-" + entry.Id.ToString("N"),
                    ObjectIcon.Resolve(entry, kind?.Icon ?? new IconObject()),
                    entry.Summary,
                    ObjectKindCatalog.ResolveDetailUri(entry),
                    meta: LandingHtml.Join(entry.Key, updated)
                ));
            }

            section.Add(list);

            return section;
        }
    }
}
