using KleeneStar.Core.WebManager;
using System;
using System.Linq;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The three personal slices of the object set the landing page names as entry paths:
    /// what is mine, what was shared with me, and what I am watching.
    /// </summary>
    /// <remarks>
    /// The slices are defined once here because two callers need the same definition and
    /// must not disagree: the entry-path card prints the size of a slice, and the page
    /// behind the card lists it. A card that promises eleven and a page that shows nine is
    /// worse than no card at all. The page does cap what it renders, but it counts through
    /// the same definition and says so when the cap bites.
    /// <para>
    /// Every builder hands back a fresh query, and the cap is a parameter rather than
    /// something a caller adds afterwards: a query that already carries paging counts the
    /// page instead of the slice - the trap the count helpers on the managers warn about.
    /// </para>
    /// </remarks>
    internal static class LandingScope
    {
        /// <summary>
        /// Builds the query of the objects that belong to the supplied identity: the
        /// issues assigned to them plus the issues they raised.
        /// </summary>
        /// <param name="identityId">The identity the slice belongs to.</param>
        /// <param name="max">The page size, or <c>0</c> for the whole slice (used when counting).</param>
        /// <returns>The query.</returns>
        public static IQuery<Model.Entities.Object> BuildMineQuery(Guid identityId, int max = 0)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Issue)
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .Where(x => x.AssigneeId == identityId || x.CreatorId == identityId)
                .OrderByDesc(x => x.Updated);

            return max > 0 ? query.WithPaging(0, max) : query;
        }

        /// <summary>
        /// Builds the query of the active objects with the supplied ids, newest change
        /// first. Backs the shared and the watched slice, both of which are named by their
        /// link rows rather than by a property of the object.
        /// </summary>
        /// <param name="ids">The ids to fetch.</param>
        /// <param name="max">The page size, or <c>0</c> for the whole slice (used when counting).</param>
        /// <returns>The query.</returns>
        public static IQuery<Model.Entities.Object> BuildIdQuery(Guid[] ids, int max = 0)
        {
            var query = new Query<Model.Entities.Object>()
                .Where(x => ids.Contains(x.Id))
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .OrderByDesc(x => x.Updated);

            return max > 0 ? query.WithPaging(0, max) : query;
        }

        /// <summary>
        /// Returns the ids of the objects shared with the supplied identity.
        /// </summary>
        /// <param name="shareManager">The share manager. Cannot be null.</param>
        /// <param name="identityId">The identity the shares point at.</param>
        /// <returns>The distinct object ids. The array may be empty.</returns>
        public static Guid[] GetSharedIds(IShareManager shareManager, Guid identityId)
        {
            var query = new Query<Model.Entities.ObjectShare>()
                .WhereEquals(x => x.IdentityId, identityId);

            return [.. shareManager.GetShares(query)
                .Select(x => x.ObjectId)
                .Distinct()];
        }

        /// <summary>
        /// Returns the ids of the objects the supplied identity is watching.
        /// </summary>
        /// <param name="watcherManager">The watcher manager. Cannot be null.</param>
        /// <param name="identityId">The watching identity.</param>
        /// <returns>The distinct object ids. The array may be empty.</returns>
        public static Guid[] GetWatchedIds(IWatcherManager watcherManager, Guid identityId)
        {
            var query = new Query<Model.Entities.ObjectWatcher>()
                .WhereEquals(x => x.IdentityId, identityId);

            return [.. watcherManager.GetWatchers(query)
                .Select(x => x.ObjectId)
                .Distinct()];
        }
    }
}
