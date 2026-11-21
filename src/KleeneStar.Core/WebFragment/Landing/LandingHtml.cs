using System;
using System.Collections.Generic;
using System.Globalization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The phrases and numbers the landing page composes: a culture-aware figure, an age in
    /// words, and the separator that joins a source to a time.
    /// </summary>
    /// <remarks>
    /// The page itself is built from WebExpress controls; what is left here is the formatting
    /// they are fed with. Keeping it in one place is what makes "5 minutes ago" read the same
    /// on the figure row, in the news list and on the timeline.
    /// </remarks>
    public static class LandingHtml
    {
        /// <summary>
        /// Renders a control into an HTML node, so a control can be returned from a fragment
        /// that hands back raw HTML.
        /// </summary>
        /// <param name="control">The control to render.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The rendered node.</returns>
        public static IHtmlNode Render(IControl control, IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return control?.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns how long ago something happened, in the coarsest unit that still says
        /// something. Reuses the phrases of the notification centre - "5 minutes ago" is the
        /// same sentence wherever it is read.
        /// </summary>
        /// <param name="moment">The moment being described, in UTC.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The age phrase.</returns>
        public static string Age(DateTime moment, IRenderControlContext renderContext)
        {
            var elapsed = DateTime.UtcNow - moment;

            if (elapsed < TimeSpan.FromMinutes(5))
            {
                return I18N.Translate(renderContext, "kleenestar.core:notification.center.age.now");
            }

            if (elapsed < TimeSpan.FromHours(1))
            {
                return Count(renderContext, "kleenestar.core:notification.center.age.minutes", (int)elapsed.TotalMinutes);
            }

            if (elapsed < TimeSpan.FromDays(1))
            {
                return Count(renderContext, "kleenestar.core:notification.center.age.hours", (int)elapsed.TotalHours);
            }

            return Count(renderContext, "kleenestar.core:notification.center.age.days", (int)elapsed.TotalDays);
        }

        /// <summary>
        /// Formats a translated pattern with a single count, never below one so an entry that
        /// just crossed a boundary does not read as "0 hours ago".
        /// </summary>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="key">The resource key of the pattern.</param>
        /// <param name="count">The count to insert.</param>
        /// <returns>The formatted phrase.</returns>
        public static string Count(IRenderControlContext renderContext, string key, int count)
        {
            return string.Format
            (
                Culture(renderContext),
                I18N.Translate(renderContext, key),
                Math.Max(1, count)
            );
        }

        /// <summary>
        /// Formats a number in the reader's culture.
        /// </summary>
        /// <param name="value">The number.</param>
        /// <param name="renderContext">The render context supplying the culture.</param>
        /// <returns>The formatted number.</returns>
        public static string Number(int value, IRenderControlContext renderContext)
        {
            return value.ToString("N0", Culture(renderContext));
        }

        /// <summary>
        /// Returns the culture of the request, falling back to the ambient one.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The culture.</returns>
        public static CultureInfo Culture(IRenderControlContext renderContext)
        {
            return renderContext?.Request?.Culture ?? CultureInfo.CurrentCulture;
        }

        /// <summary>
        /// Joins the supplied parts with the middot separator the page uses between a source
        /// and a time.
        /// </summary>
        /// <param name="parts">The parts to join; empty ones are dropped.</param>
        /// <returns>The joined text.</returns>
        public static string Join(params string[] parts)
        {
            var kept = new List<string>();

            foreach (var part in parts ?? [])
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    kept.Add(part);
                }
            }

            return string.Join(" · ", kept);
        }

        /// <summary>
        /// Builds the chip naming the state an object is in, coloured by its status category -
        /// the same chip the start page of a class puts at the end of its rows.
        /// </summary>
        /// <param name="category">The status category, or <see langword="null"/>.</param>
        /// <returns>The chip, or <see langword="null"/> when the object has no state.</returns>
        public static IControl StateChip(Model.Entities.StatusCategory category)
        {
            if (category is null)
            {
                return null;
            }

            var tone = "ks-co-tone-" + (WebRestApi.ObjectBoardProjection.CategoryColorCss(category) ?? "wx-color-secondary").Replace("wx-color-", string.Empty);

            return new ControlText
            {
                Text = _ => WebRestApi.ObjectBoardProjection.CategoryLabel(category),
                Format = _ => TypeFormatText.Span,
                Classes = ["ks-co-chip", tone]
            };
        }
    }
}
