using System;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Translates the class a template instantiates into the filter id the quick filter renders
    /// and the template table resolves, and back again.
    /// </summary>
    /// <remarks>
    /// The prefix keeps the id apart from the other filter kinds the table receives in the same
    /// parameter — the category filters and the state quick filters — so each is recognized by
    /// whoever is responsible for it.
    /// </remarks>
    public static class TemplateClassFilter
    {
        /// <summary>
        /// The prefix that marks a class filter.
        /// </summary>
        private const string Prefix = "cls-";

        /// <summary>
        /// Returns the filter id representing the given class.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The filter id.</returns>
        public static string ToFilterId(Guid classId)
        {
            return Prefix + classId.ToString();
        }

        /// <summary>
        /// Reads the class a filter id represents.
        /// </summary>
        /// <param name="filterId">The filter id to read.</param>
        /// <param name="classId">
        /// When this method returns, contains the id of the class, or <see cref="Guid.Empty"/>
        /// when the id does not represent one.
        /// </param>
        /// <returns>True when the id is a class filter; otherwise false.</returns>
        public static bool TryGetClass(string filterId, out Guid classId)
        {
            classId = Guid.Empty;

            if (string.IsNullOrEmpty(filterId) || !filterId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return Guid.TryParse(filterId[Prefix.Length..], out classId) && classId != Guid.Empty;
        }
    }
}
