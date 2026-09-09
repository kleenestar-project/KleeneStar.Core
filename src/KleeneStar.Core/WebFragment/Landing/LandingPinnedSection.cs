using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebIcon;
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
    /// pinning is done where the object lives - its tag card - rather than in a settings page
    /// for the landing page. Without any pinned object the area still renders its heading and
    /// says how something gets here: an empty area that explains itself is what a newcomer
    /// needs, an area that disappears teaches nothing.
    /// </remarks>
    internal static class LandingPinnedSection
    {
        /// <summary>
        /// The maximum number of pinned objects shown.
        /// </summary>
        private const int MaxItems = 6;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="objectManager">The object manager used to resolve the pinned objects.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The section control.</returns>
        public static IControl Build
        (
            IObjectTagManager tagManager,
            IObjectManager objectManager,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            var pinned = LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Pinned, MaxItems);

            var section = new ControlSection("landing-pinned")
            {
                Header = _ => "kleenestar.core:landing.pinned.card",
                HeaderIcon = _ => new IconThumbtack(),
                Note = _ => "kleenestar.core:landing.pinned.hint",
                Layout = _ => TypeLayoutSection.Rule
            };

            if (pinned.Count > 0)
            {
                section.Badge = _ => LandingHtml.Number(pinned.Count, renderContext);
                section.BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary);
                section.Add(BuildTiles(pinned, renderContext));
            }
            else
            {
                section.Add(new ControlText("landing-pinned-empty")
                {
                    Text = _ => "kleenestar.core:landing.pinned.empty",
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });
            }

            return section;
        }

        /// <summary>
        /// Builds the tiles of the pinned entries.
        /// </summary>
        /// <param name="pinned">The pinned objects.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The tile control.</returns>
        private static IControl BuildTiles(IReadOnlyList<Model.Entities.Object> pinned, IRenderControlContext renderContext)
        {
            // a grid of fields rather than a row of framed cards: what the organization keeps
            // in sight is one set, and a frame around each entry would read as five unrelated
            // documents that happen to sit next to each other
            var grid = new ControlGroup("landing-pinned-grid")
            {
                Columns = _ => 2,
                Spacing = _ => TypeSpacingGroup.Wide
            };

            foreach (var entry in pinned)
            {
                grid.Add(BuildEntry(entry, renderContext));
            }

            return grid;
        }

        /// <summary>
        /// Builds one pinned entry: its icon, its summary as the link into it, the sentence
        /// beneath, and the line saying how current it is.
        /// </summary>
        /// <remarks>
        /// The route is resolved through <see cref="ObjectKindCatalog"/>, so a pinned document
        /// opens in the document view and a pinned issue in the issue view without this section
        /// knowing either kind.
        /// </remarks>
        /// <param name="entry">The pinned object.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The entry.</returns>
        private static IControl BuildEntry(Model.Entities.Object entry, IRenderControlContext renderContext)
        {
            var id = entry.Id.ToString("N");
            var kind = ObjectKindCatalog.GetKind(entry.Kind);

            var panel = new ControlPanel("landing-pinned-" + id);

            panel.Add(new ControlLink("landing-pinned-open-" + id)
            {
                Text = _ => entry.Summary,
                Tooltip = _ => entry.Key,
                Icon = _ => ObjectIcon.Resolve(entry, kind?.Icon ?? new IconObject()),
                Uri = _ => ObjectKindCatalog.ResolveDetailUri(entry)
            });

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                panel.Add(new ControlText("landing-pinned-description-" + id)
                {
                    Text = _ => entry.Description,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Format = _ => TypeFormatText.Paragraph
                });
            }

            var updated = string.Format
            (
                LandingHtml.Culture(renderContext),
                I18N.Translate(renderContext, "kleenestar.core:landing.pinned.updated"),
                entry.Updated.ToString("d", LandingHtml.Culture(renderContext))
            );

            panel.Add(new ControlText("landing-pinned-updated-" + id)
            {
                Text = _ => LandingHtml.Join(entry.Key, updated),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Code
            });

            return panel;
        }
    }
}
