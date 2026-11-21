using System;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The value a template or hierarchy card of the object creation wizard submits as
    /// <c>TemplateId</c>, and the value of the root selection of the hierarchy wizard.
    /// </summary>
    /// <remarks>
    /// A template serving several classes is offered once per class, and the tile selection
    /// needs a distinct value per card: the card under the owning class carries the template id,
    /// a card under a further class the template id followed by <c>@</c> and the class id. In the
    /// object creation wizard the class travels separately as <c>ClassId</c>, so the readers take
    /// the template id alone. A hierarchy card carries <see cref="HierarchyPrefix"/> followed by
    /// the hierarchy id; it names no template id.
    /// </remarks>
    public static class TemplateChoice
    {
        /// <summary>
        /// The separator between the template id and the class id.
        /// </summary>
        public const char Separator = '@';

        /// <summary>
        /// The prefix of the value of a hierarchy card.
        /// </summary>
        public const string HierarchyPrefix = "hierarchy:";

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
        /// Returns the value of the card offering a hierarchy.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <returns>The card value.</returns>
        public static string HierarchyValue(Guid hierarchyId)
        {
            return HierarchyPrefix + hierarchyId;
        }

        /// <summary>
        /// Reads the template id of a card value; a plain template id reads as itself.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <param name="templateId">When this method returns, the template id, or <see cref="Guid.Empty"/>.</param>
        /// <returns><see langword="true"/> when the value names a template.</returns>
        public static bool TryParse(string value, out Guid templateId)
        {
            return TryParse(value, out templateId, out _);
        }

        /// <summary>
        /// Reads the template id and the class id of a card value.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <param name="templateId">When this method returns, the template id, or <see cref="Guid.Empty"/>.</param>
        /// <param name="classId">When this method returns, the class id the value names, or null
        /// for the class the template is bound to.</param>
        /// <returns><see langword="true"/> when the value names a template.</returns>
        public static bool TryParse(string value, out Guid templateId, out Guid? classId)
        {
            templateId = Guid.Empty;
            classId = null;

            if (string.IsNullOrWhiteSpace(value) || value.StartsWith(HierarchyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var separator = value.IndexOf(Separator);
            var text = separator >= 0 ? value[..separator] : value;

            if (separator >= 0 && Guid.TryParse(value[(separator + 1)..].Trim(), out var parsedClass) && parsedClass != Guid.Empty)
            {
                classId = parsedClass;
            }

            return Guid.TryParse(text.Trim(), out templateId) && templateId != Guid.Empty;
        }

        /// <summary>
        /// Reads the hierarchy id of a card value.
        /// </summary>
        /// <param name="value">The submitted value.</param>
        /// <param name="hierarchyId">When this method returns, the hierarchy id, or <see cref="Guid.Empty"/>.</param>
        /// <returns><see langword="true"/> when the value names a hierarchy.</returns>
        public static bool TryParseHierarchy(string value, out Guid hierarchyId)
        {
            hierarchyId = Guid.Empty;

            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().StartsWith(HierarchyPrefix, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(value.Trim()[HierarchyPrefix.Length..], out hierarchyId)
                && hierarchyId != Guid.Empty;
        }
    }
}
