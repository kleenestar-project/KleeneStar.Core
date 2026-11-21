using System;
using System.Text;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Translates a template category into the filter id the sidebar renders and the template
    /// table resolves, and back again.
    /// </summary>
    /// <remarks>
    /// A template category is free text, while a filter id travels as a DOM element id and as a
    /// comma-separated value of the table's <c>f</c> parameter — so it may carry neither spaces
    /// nor commas, and it must survive the round trip unchanged, because the table compares the
    /// decoded value against the stored category. Hex-encoding the text satisfies both: the id is
    /// opaque but lossless and collision-free, which a sanitizing slug would not be (two
    /// categories differing only in punctuation would collapse into one filter).
    /// </remarks>
    public static class TemplateCategoryFilter
    {
        /// <summary>
        /// The prefix that marks a category filter, matching the convention the other overviews
        /// use for their category filters.
        /// </summary>
        private const string Prefix = "cat-";

        /// <summary>
        /// Returns the filter id representing the given category.
        /// </summary>
        /// <param name="category">The category text. Cannot be null or empty.</param>
        /// <returns>The filter id.</returns>
        public static string ToFilterId(string category)
        {
            return Prefix + Convert.ToHexString(Encoding.UTF8.GetBytes(category ?? string.Empty));
        }

        /// <summary>
        /// Reads the category a filter id represents.
        /// </summary>
        /// <param name="filterId">The filter id to read.</param>
        /// <param name="category">
        /// When this method returns, contains the decoded category, or null when the id does not
        /// represent one.
        /// </param>
        /// <returns>True when the id is a category filter; otherwise false.</returns>
        public static bool TryGetCategory(string filterId, out string category)
        {
            category = null;

            if (string.IsNullOrEmpty(filterId) || !filterId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                category = Encoding.UTF8.GetString(Convert.FromHexString(filterId[Prefix.Length..]));
            }
            catch (FormatException)
            {
                // an id that is not valid hex was not produced here and names no category
                return false;
            }

            return !string.IsNullOrEmpty(category);
        }
    }
}
