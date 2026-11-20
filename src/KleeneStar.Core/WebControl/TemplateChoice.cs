using System;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The value a template card of the object creation wizard submits as <c>TemplateId</c>.
    /// </summary>
    /// <remarks>
    /// A template serving several classes is offered once per class, and the tile selection
    /// needs a distinct value per card: the card under the owning class carries the template id,
    /// a card under a further class the template id followed by <c>@</c> and the class id. The
    /// class itself travels separately as <c>ClassId</c>, so the readers take the template id
    /// alone.
    /// </remarks>
    public static class TemplateChoice
    {
        /// <summary>
        /// The separator between the template id and the class id.
        /// </summary>
        public const char Separator = '@';

        /// <summary>
        /// Returns the value of the card offering a template for a class.
        /// </summary>
        /// <param name="templateId">The template.</param>
        /// <param name="classId">The class the card is offered for.</param>
        /// <param name="ownerId">The class the template is bound to.</param>
        /// <returns>The card value.</returns>
        public static string Value(Guid templateId, Guid classId, Guid ownerId)
        {
            return classId == ownerId
                ? templateId.ToString()
                : $"{templateId}{Separator}{classId}";
        }

        /// <summary>
        /// Reads the template id of a card value; a plain template id reads as itself.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <param name="templateId">When this method returns, the template id, or <see cref="Guid.Empty"/>.</param>
        /// <returns><see langword="true"/> when the value names a template.</returns>
        public static bool TryParse(string value, out Guid templateId)
        {
            templateId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var separator = value.IndexOf(Separator);
            var text = separator >= 0 ? value[..separator] : value;

            return Guid.TryParse(text.Trim(), out templateId) && templateId != Guid.Empty;
        }
    }
}
