using KleeneStar.Core.WebQuickfilter;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Applies the query surface of a Kanban board — the search term and the quickfilter
    /// chips of its header — to the objects the board would show.
    /// </summary>
    /// <remarks>
    /// The board resolves its cards in memory and hands them through
    /// <see cref="RestApiObjectKindKanban.ApplyQuickfilter"/>, which is the single hook it
    /// offers; search and chips are therefore applied together here rather than through the
    /// query. Keeping it in one place means the issue board and the asset board cannot
    /// drift apart on what a chip or a search term means.
    ///
    /// The archived scope is deliberately absent. The board collects active objects before
    /// this runs, so a chip for the archived history could not be honoured — and a chip that
    /// shows but does nothing is worse than one that is not offered.
    /// </remarks>
    public static class ObjectKindBoardFilter
    {
        /// <summary>The quickfilter id prefix shared by every board chip.</summary>
        public const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the starred chip.</summary>
        public const string StarredId = IdPrefix + "starred";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        public const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the created-by-me chip.</summary>
        public const string CreatedId = IdPrefix + "created";

        /// <summary>
        /// Narrows the objects by the search term of the <c>q</c> parameter and the
        /// quickfilter chips of the <c>f</c> parameter, including the filters the user
        /// defined for the view.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request carrying the query surface and the caller.</param>
        /// <param name="viewKey">
        /// The key the user-defined filters of this view are stored under. The board shares
        /// it with the kind's list, so a filter defined in either place serves both.
        /// </param>
        /// <returns>The matching objects.</returns>
        public static IEnumerable<Model.Entities.Object> Apply
        (
            IEnumerable<Model.Entities.Object> objects,
            IRequest request,
            string viewKey
        )
        {
            var filters = request?.GetParameter("f")?.Value?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
            var selected = new HashSet<string>(filters, StringComparer.OrdinalIgnoreCase);

            if (selected.Count > 0)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

                if (selected.Contains(StarredId))
                {
                    var starredIds = CoreHub.ObjectManager.GetFavoriteObjects(ownerId)
                        .Select(x => x.Id)
                        .ToHashSet();

                    objects = objects.Where(x => starredIds.Contains(x.Id));
                }

                if (selected.Contains(MineId))
                {
                    objects = objects.Where(x => x.AssigneeId == ownerId);
                }

                if (selected.Contains(CreatedId))
                {
                    objects = objects.Where(x => x.CreatorId == ownerId);
                }
            }

            objects = ApplySearch(objects, request);

            // the filters the user defined are resolved from storage rather than from a chip
            // id handled above, and narrow further so they combine with the scopes and the
            // search
            return CustomQuickfilterSupport.Apply(filters, objects, viewKey);
        }

        /// <summary>
        /// Narrows the objects to those whose key, summary or description contains the
        /// search term. An absent or blank term leaves the set unchanged.
        /// </summary>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request carrying the search term in <c>q</c>.</param>
        /// <returns>The matching objects.</returns>
        private static IEnumerable<Model.Entities.Object> ApplySearch(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            var search = request?.GetParameter("q")?.Value?.Trim();

            // the client sends the literal "null" when a search box is cleared
            if (string.IsNullOrWhiteSpace(search) || search == "null")
            {
                return objects;
            }

            return objects.Where
            (
                x => (x.Key ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                  || (x.Summary ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                  || (x.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
