using KleeneStar.Core.WebManager;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The reserved object labels the landing page reads. A label is an ordinary
    /// <see cref="Model.Entities.ObjectTag"/> - the same rows the tag card of an object
    /// writes - so curating the landing page needs no separate editor and no separate
    /// permission: whoever may label an object may decide what the organization sees on
    /// its way in.
    /// </summary>
    /// <remarks>
    /// The pinned area and the help area differ only in which label they read. Both are
    /// therefore free of content of their own: an org chart, a guideline, a FAQ page and a
    /// first-steps walkthrough are all just objects (pages) of the installation, and the
    /// label is what promotes one of them to the landing page. Nothing here creates or
    /// deletes a label - the landing page is a reader.
    /// <para>
    /// Labels are matched case-insensitively, so <c>pinned</c>, <c>Pinned</c> and
    /// <c>PINNED</c> are the same label. They are deliberately English and stable: the
    /// display text of the area comes from the resource files, never from the label.
    /// </para>
    /// </remarks>
    public static class LandingLabel
    {
        /// <summary>
        /// The label that pins an object to the landing page - the org chart, the central
        /// guidelines, the documents that must not have to be searched for.
        /// </summary>
        public const string Pinned = "Pinned";

        /// <summary>
        /// The label of the compact how-to pages of the help area.
        /// </summary>
        public const string Help = "Help";

        /// <summary>
        /// The label of the frequently-asked-questions pages.
        /// </summary>
        public const string Faq = "FAQ";

        /// <summary>
        /// The label of the pages that walk a newcomer through creating an object, working
        /// with a template, and the basic functions.
        /// </summary>
        public const string FirstSteps = "First Steps";

        /// <summary>
        /// Returns the active objects carrying the supplied label, ordered by summary and
        /// capped at <paramref name="max"/>.
        /// </summary>
        /// <remarks>
        /// Resolved in two steps rather than through a join: the label rows name the object
        /// ids, and a single object query then fetches those ids. The second query filters
        /// on state as well, so an archived page drops off the landing page without anyone
        /// having to remember to strip its label.
        /// </remarks>
        /// <param name="tagManager">The tag manager holding the label rows. Cannot be null.</param>
        /// <param name="objectManager">The object manager used to resolve the labelled objects. Cannot be null.</param>
        /// <param name="label">The reserved label to read.</param>
        /// <param name="max">The maximum number of objects to return.</param>
        /// <returns>The labelled objects. The list may be empty.</returns>
        public static IReadOnlyList<Model.Entities.Object> Resolve
        (
            IObjectTagManager tagManager,
            IObjectManager objectManager,
            string label,
            int max
        )
        {
            if (tagManager is null || objectManager is null || string.IsNullOrWhiteSpace(label) || max <= 0)
            {
                return [];
            }

            var ids = ResolveIds(tagManager, label);

            if (ids.Length == 0)
            {
                return [];
            }

            var query = new Query<Model.Entities.Object>()
                .Where(x => ids.Contains(x.Id))
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .OrderByAsc(x => x.Summary)
                .WithPaging(0, max);

            return [.. objectManager.GetObjects(query)];
        }

        /// <summary>
        /// Returns the ids of the objects carrying the supplied label.
        /// </summary>
        /// <param name="tagManager">The tag manager holding the label rows. Cannot be null.</param>
        /// <param name="label">The reserved label to read.</param>
        /// <returns>The distinct object ids. The array may be empty.</returns>
        private static Guid[] ResolveIds(IObjectTagManager tagManager, string label)
        {
            var query = new Query<Model.Entities.ObjectTag>()
                .WhereEqualsIgnoreCase(x => x.Name, label);

            return [.. tagManager.GetTags(query)
                .Select(x => x.ObjectId)
                .Distinct()];
        }
    }
}
