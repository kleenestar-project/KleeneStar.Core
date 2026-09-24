using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The one line every list of the landing page is made of: a small icon, the title as a
    /// link, an optional one-line teaser, a quiet meta line and an optional chip at the end.
    /// </summary>
    /// <remarks>
    /// The page used to give every kind of content a shape of its own - news as big tiles with
    /// the class picture, pinned pages as columns of full descriptions, help split into three
    /// narrow columns, an accordion and a step list - and read as a collage. One row shape for
    /// all of them is what lets the eye run down a column. The look lives in
    /// <c>kleenestar.css</c> under <c>.ks-landing-row</c>; the teaser is clipped to one line
    /// there, so a long description costs a line and not a paragraph.
    /// </remarks>
    internal static class LandingRow
    {
        /// <summary>
        /// Builds a row.
        /// </summary>
        /// <param name="id">The id of the row.</param>
        /// <param name="icon">The icon in front of the title.</param>
        /// <param name="title">The title, shown as the link.</param>
        /// <param name="uri">Where the title leads.</param>
        /// <param name="meta">The meta line beneath the title, or <see langword="null"/>.</param>
        /// <param name="teaser">The one-line teaser, or <see langword="null"/>.</param>
        /// <param name="trailing">A control at the end of the row (a status chip), or <see langword="null"/>.</param>
        /// <returns>The row.</returns>
        public static IControl Build
        (
            string id,
            IIcon icon,
            string title,
            IUri uri,
            string meta = null,
            string teaser = null,
            IControl trailing = null
        )
        {
            var row = new ControlPanel(id) { Classes = ["ks-landing-row"] };

            row.Add(new ControlIcon { Icon = _ => icon, Classes = ["ks-landing-row-icon"] });

            var body = new ControlPanel { Classes = ["ks-landing-row-body"] };

            body.Add(new ControlLink
            {
                Text = _ => title,
                Uri = _ => uri,
                Classes = ["ks-landing-row-title"]
            });

            if (!string.IsNullOrWhiteSpace(teaser))
            {
                body.Add(new ControlText
                {
                    Text = _ => teaser,
                    Format = _ => TypeFormatText.Span,
                    Classes = ["ks-landing-row-teaser"]
                });
            }

            if (!string.IsNullOrWhiteSpace(meta))
            {
                body.Add(new ControlText
                {
                    Text = _ => meta,
                    Format = _ => TypeFormatText.Small,
                    Classes = ["ks-landing-row-meta"]
                });
            }

            row.Add(body);

            if (trailing is not null)
            {
                row.Add(trailing);
            }

            return row;
        }

        /// <summary>
        /// Builds the list the rows of a section stand in.
        /// </summary>
        /// <param name="id">The id of the list.</param>
        /// <returns>The list.</returns>
        public static ControlPanel List(string id)
        {
            return new ControlPanel(id) { Classes = ["ks-landing-rows"] };
        }

        /// <summary>
        /// Builds the quiet line a section says it has nothing with.
        /// </summary>
        /// <param name="id">The id of the line.</param>
        /// <param name="text">The text or i18n key.</param>
        /// <returns>The line.</returns>
        public static IControl Empty(string id, string text)
        {
            return new ControlText(id)
            {
                Text = _ => text,
                Format = _ => TypeFormatText.Paragraph,
                Classes = ["ks-landing-empty"]
            };
        }

        /// <summary>
        /// Builds the link beneath a list that leads to the whole of it.
        /// </summary>
        /// <param name="id">The id of the link.</param>
        /// <param name="text">The text or i18n key.</param>
        /// <param name="uri">Where it leads.</param>
        /// <returns>The link.</returns>
        public static IControl More(string id, string text, IUri uri)
        {
            return new ControlLink(id)
            {
                Text = _ => text,
                Uri = _ => uri,
                Classes = ["ks-landing-more"]
            };
        }

        /// <summary>
        /// Builds a small caption that divides the groups inside one section.
        /// </summary>
        /// <param name="text">The text or i18n key.</param>
        /// <returns>The caption.</returns>
        public static IControl Caption(string text)
        {
            return new ControlText
            {
                Text = _ => text,
                Format = _ => TypeFormatText.Small,
                Classes = ["ks-landing-caption"]
            };
        }
    }
}
