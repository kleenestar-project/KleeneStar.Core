using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Translates the object type of a class into the filter id the class sidebar renders and
    /// the class table, tile and list endpoints resolve, and back again.
    /// </summary>
    /// <remarks>
    /// The prefix keeps the id apart from the other filter kinds the endpoints receive in the
    /// same parameter - the state quick filters - so each is recognized by whoever is
    /// responsible for it. The kind travels as its key, not as a label: the set of kinds is
    /// open, and a key of an add-on kind filters the same way a core one does.
    /// </remarks>
    public static class ClassKindFilter
    {
        /// <summary>
        /// The prefix that marks an object type filter.
        /// </summary>
        private const string Prefix = "kind-";

        /// <summary>
        /// The filter group the object type entries share, so picking one replaces the
        /// previous choice rather than adding to it.
        /// </summary>
        public const string FilterGroup = "class-kind";

        /// <summary>
        /// Returns the filter id representing the given object type.
        /// </summary>
        /// <param name="kind">The key of the object type.</param>
        /// <returns>The filter id.</returns>
        public static string ToFilterId(string kind)
        {
            return Prefix + ObjectKind.Normalize(kind);
        }

        /// <summary>
        /// Reads the object type a filter id represents.
        /// </summary>
        /// <param name="filterId">The filter id to read.</param>
        /// <param name="kind">
        /// When this method returns, contains the normalized key of the object type, or
        /// <see langword="null"/> when the id does not represent one.
        /// </param>
        /// <returns>True when the id is an object type filter; otherwise false.</returns>
        public static bool TryGetKind(string filterId, out string kind)
        {
            kind = null;

            if (string.IsNullOrEmpty(filterId)
                || !filterId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(filterId[Prefix.Length..]))
            {
                return false;
            }

            kind = ObjectKind.Normalize(filterId[Prefix.Length..]);

            return true;
        }

        /// <summary>
        /// Narrows the query to the classes of the object type the filters name.
        /// </summary>
        /// <param name="filters">The filter ids the request carries.</param>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query, or the query itself when no object type is named.</returns>
        public static IQuery<Class> Apply(IEnumerable<string> filters, IQuery<Class> query)
        {
            foreach (var kind in (filters ?? [])
                .Select(f => TryGetKind(f, out var k) ? k : null)
                .Where(k => k is not null)
                .Distinct())
            {
                // a class that names no kind is of the default kind (ObjectKind.Normalize), so
                // the default entry has to find it as well
                query = kind == ObjectKind.Default
                    ? query.Where(x => x.Kind == kind || x.Kind == null || x.Kind == "")
                    : query.Where(x => x.Kind == kind);
            }

            return query;
        }
    }
}
