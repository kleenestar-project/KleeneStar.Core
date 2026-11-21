using KleeneStar.Model.Entities;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Builds the badge one tag is shown as, wherever it is shown.
    /// </summary>
    /// <remarks>
    /// A tag is recognized by its colour as much as by its text, so the colour a tag without a
    /// stored one falls back to has to be the same on the issue property card as under the text
    /// of a document. Two derivations would give the same tag two colours and quietly undo that.
    /// </remarks>
    internal static class ObjectTagBadge
    {
        /// <summary>
        /// Builds the badge for a single tag. The background is the tag's stored
        /// <see cref="ObjectTag.Color"/>, or a colour derived from its name when none is stored;
        /// the text is white for contrast against the coloured background.
        /// </summary>
        /// <param name="tag">The tag to render.</param>
        /// <param name="idPrefix">The prefix of the element id, so two surfaces showing the same
        /// tag on one page do not collide.</param>
        /// <returns>The badge control.</returns>
        public static IControl Create(ObjectTag tag, string idPrefix)
        {
            var color = string.IsNullOrWhiteSpace(tag.Color) ? DeriveColor(tag.Name) : tag.Color;

            return new ControlBadge(idPrefix + tag.Id.ToString("N"))
            {
                Value = _ => tag.Name,
                Styles = ["background-color: " + color + "; color: #fff; border-radius: 0.5em; padding: 0.2em 0.6em;"]
            };
        }

        /// <summary>
        /// Derives a deterministic six-digit hex colour from a tag name so tags without a
        /// stored colour still get a stable, distinct badge colour across requests.
        /// </summary>
        /// <param name="name">The tag name.</param>
        /// <returns>A CSS hex colour string of the form <c>#RRGGBB</c>.</returns>
        public static string DeriveColor(string name)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in name ?? string.Empty)
                {
                    hash = (hash * 31) + ch;
                }

                return "#" + (hash & 0x00FFFFFF).ToString("x6");
            }
        }
    }
}
