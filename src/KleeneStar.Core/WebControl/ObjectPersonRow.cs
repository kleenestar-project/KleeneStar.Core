using System;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// One person of the people card on the object page: a round picture (the portrait, else the
    /// initials), the role in small letters above the name, optionally a status chip and an
    /// action at the end of the row.
    /// </summary>
    /// <remarks>
    /// A row without a person (<see cref="Muted"/>) draws an empty, dashed circle and the name in
    /// the secondary colour, so "nobody yet" reads as a state rather than as a missing value. The
    /// look lives in <c>kleenestar.css</c> under <i>people card</i>.
    /// </remarks>
    public sealed class ObjectPersonRow : Control
    {
        /// <summary>
        /// Gets or sets the name shown, or the text (an i18n key) of a muted row.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the role above the name, an i18n key or text.
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// Gets or sets the address of the portrait, or <see langword="null"/> for the initials.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Gets or sets whether the row stands for nobody.
        /// </summary>
        public bool Muted { get; set; }

        /// <summary>
        /// Gets or sets whether the picture stands for a group rather than a person.
        /// </summary>
        public bool Group { get; set; }

        /// <summary>
        /// Gets or sets the status chip, an i18n key or text, or <see langword="null"/>.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the tone of the status chip: <c>success</c>, <c>danger</c> or <c>pending</c>.
        /// </summary>
        public string StatusTone { get; set; }

        /// <summary>
        /// Gets or sets the action at the end of the row, or <see langword="null"/>.
        /// </summary>
        public IControl Action { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public ObjectPersonRow(string id)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var row = new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("ks-person", Muted ? "ks-person-muted" : null, GetClasses(renderContext)),
                Style = GetStyles(renderContext)
            };

            var avatar = new HtmlElementTextSemanticsSpan()
            {
                Class = Css.Concatenate("ks-person-avatar", Group ? "ks-person-avatar-group" : null)
            };

            avatar.AddUserAttribute("aria-hidden", "true");

            if (!Muted && !string.IsNullOrWhiteSpace(Image))
            {
                avatar.Add(new HtmlElementMultimediaImg() { Src = Image, Alt = string.Empty });
            }
            else if (!Muted)
            {
                avatar.Add(new HtmlText(Initials(Name)));
            }

            row.Add(avatar);

            var text = new HtmlElementTextSemanticsSpan()
            {
                Class = "ks-person-text"
            };

            if (!string.IsNullOrWhiteSpace(Caption))
            {
                text.Add(new HtmlElementTextSemanticsSpan(new HtmlText(I18N.Translate(renderContext, Caption)))
                {
                    Class = "ks-person-role"
                });
            }

            // a person's name is shown as stored; only the text of a muted row is a key
            text.Add(new HtmlElementTextSemanticsSpan(new HtmlText(Muted ? I18N.Translate(renderContext, Name ?? string.Empty) : Name ?? string.Empty))
            {
                Class = "ks-person-name"
            });

            row.Add(text);

            if (!string.IsNullOrWhiteSpace(Status))
            {
                row.Add(new HtmlElementTextSemanticsSpan(new HtmlText(I18N.Translate(renderContext, Status)))
                {
                    Class = Css.Concatenate("ks-person-status", string.IsNullOrWhiteSpace(StatusTone) ? null : "ks-person-status-" + StatusTone)
                });
            }

            if (Action?.Render(renderContext, visualTree) is { } action)
            {
                row.Add(new HtmlElementTextSemanticsSpan(action)
                {
                    Class = "ks-person-action"
                });
            }

            return row;
        }

        /// <summary>
        /// Returns the initials of a name: the first letters of its first two words.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The initials, upper-cased; empty for a blank name.</returns>
        public static string Initials(string name)
        {
            var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Concat(words.Take(2).Select(x => char.ToUpperInvariant(x[0])));
        }
    }
}
