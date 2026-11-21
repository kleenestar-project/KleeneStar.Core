using KleeneStar.Core.WebQuickfilter;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the signatures read
// naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The personal scopes the quickfilter bar of a kind overview offers — starred, assigned to
    /// me, created by me, archived — and their translation into a query.
    /// </summary>
    /// <remarks>
    /// The bar is one control per view while the rows come from several endpoints (the table, the
    /// list, the tile view), so every one of them has to understand the same chip ids. They are
    /// named here once, together with the narrowing they stand for, rather than being written out
    /// per endpoint — an endpoint that does not know an id silently ignores the chip, which is how
    /// the list and the tile view came to answer a click with the unfiltered result.
    ///
    /// The lifecycle scope is applied whether or not the archived chip is on: without it the two
    /// halves of the lifecycle are shown mixed, which is not a state the overview has a name for.
    /// </remarks>
    public static class ObjectKindQuickfilter
    {
        /// <summary>The quickfilter id prefix shared by every chip.</summary>
        public const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the starred chip.</summary>
        public const string StarredId = IdPrefix + "starred";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        public const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the created-by-me chip.</summary>
        public const string CreatedId = IdPrefix + "created";

        /// <summary>Quickfilter id of the archived chip.</summary>
        public const string ArchivedId = IdPrefix + "archived";

        /// <summary>
        /// Narrows a query by the chips the client reports as active, and by the quickfilters the
        /// user defined for the view.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <param name="filters">The quickfilter ids reported as active.</param>
        /// <param name="request">The request the calling identity is taken from.</param>
        /// <param name="viewKey">
        /// The key the user-defined quickfilters of the view are stored under, or
        /// <see langword="null"/> for a view that offers none.
        /// </param>
        /// <returns>The narrowed query.</returns>
        public static IQuery<ObjectEntity> Apply
        (
            IQuery<ObjectEntity> query,
            IEnumerable<string> filters,
            IRequest request,
            string viewKey
        )
        {
            var selected = new HashSet<string>(filters ?? [], StringComparer.OrdinalIgnoreCase);
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            // the archived chip flips the lifecycle scope: without it the view shows the active
            // objects, with it the archived history
            var state = selected.Contains(ArchivedId)
                ? Model.Entities.WorkspaceState.Archived
                : Model.Entities.WorkspaceState.Active;
            query = query.Where(x => x.State == state);

            if (selected.Contains(StarredId))
            {
                // starring is a mark on the caller's visit row rather than a property of the
                // object, so the ids are read first and the query narrowed to them
                var starred = CoreHub.ObjectManager.GetFavoriteObjects(ownerId)
                    .Select(x => x.Id)
                    .ToList();

                query = query.Where(x => starred.Contains(x.Id));
            }

            if (selected.Contains(MineId))
            {
                query = query.Where(x => x.AssigneeId == ownerId);
            }

            if (selected.Contains(CreatedId))
            {
                query = query.Where(x => x.CreatorId == ownerId);
            }

            if (!string.IsNullOrWhiteSpace(viewKey))
            {
                query = CustomQuickfilterSupport.Apply(filters, query, viewKey);
            }

            return query;
        }
    }
}
