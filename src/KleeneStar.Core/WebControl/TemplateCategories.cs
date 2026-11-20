using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The categories of a template, stored in <see cref="Model.Entities.Template.Category"/> as
    /// one text separated by <see cref="Separator"/>, the value format of the tag input the
    /// template wizard edits them with.
    /// </summary>
    public static class TemplateCategories
    {
        /// <summary>
        /// The separator between two categories.
        /// </summary>
        public const char Separator = ';';

        /// <summary>
        /// The length of the stored text (the column length).
        /// </summary>
        public const int MaxLength = 128;

        /// <summary>
        /// Reads the categories of a stored or submitted text.
        /// </summary>
        /// <param name="value">The text; null reads as none.</param>
        /// <returns>The trimmed categories in order, each once ignoring case.</returns>
        public static IReadOnlyList<string> Parse(string value)
        {
            return [.. (value ?? string.Empty)
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Writes categories as the stored text.
        /// </summary>
        /// <param name="categories">The categories.</param>
        /// <returns>The text, or null when there is no category.</returns>
        public static string Format(IEnumerable<string> categories)
        {
            var text = string.Join(Separator, (categories ?? [])
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));

            return text.Length == 0 ? null : text;
        }

        /// <summary>
        /// Brings a submitted text into the stored form: trimmed, without empty or repeated
        /// categories, null when nothing remains.
        /// </summary>
        /// <param name="value">The submitted text.</param>
        /// <returns>The stored text.</returns>
        public static string Normalize(string value)
        {
            return Format(Parse(value));
        }

        /// <summary>
        /// Determines whether a stored text names a category, ignoring case.
        /// </summary>
        /// <param name="value">The stored text.</param>
        /// <param name="category">The category.</param>
        /// <returns><see langword="true"/> when the category is one of the text.</returns>
        public static bool Contains(string value, string category)
        {
            return Parse(value).Contains(category?.Trim(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
