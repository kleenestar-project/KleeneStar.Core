using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing per-identity saved searches — named, reusable
    /// queries over the object model that back the global "search over all workspaces"
    /// navigation dropdown (recently used) and the search-page sidebar (starred + all).
    /// </summary>
    /// <remarks>
    /// Implementations should ensure thread-safety if used in a multi-threaded environment.
    /// </remarks>
    public interface ISavedSearchManager : IComponentManager
    {
        /// <summary>An event that fires when a saved search is added.</summary>
        event EventHandler<SavedSearch> SavedSearchAdded;

        /// <summary>An event that fires when a saved search is updated.</summary>
        event EventHandler<SavedSearch> SavedSearchUpdated;

        /// <summary>An event that fires when a saved search is removed.</summary>
        event EventHandler<SavedSearch> SavedSearchRemoved;

        /// <summary>
        /// Returns a saved search by its id, or <see langword="null"/> when none exists.
        /// </summary>
        /// <param name="savedSearchId">The id of the saved search.</param>
        /// <returns>The saved search, or <see langword="null"/>.</returns>
        SavedSearch GetSavedSearch(Guid savedSearchId);

        /// <summary>
        /// Returns a saved search by its id parameter, or <see langword="null"/> when none exists.
        /// </summary>
        /// <param name="savedSearchId">The id parameter of the saved search.</param>
        /// <returns>The saved search, or <see langword="null"/>.</returns>
        SavedSearch GetSavedSearch(SavedSearchIdParameter savedSearchId);

        /// <summary>
        /// Returns the saved searches matching the given query.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <returns>The matching saved searches.</returns>
        IEnumerable<SavedSearch> GetSavedSearches(IQuery<SavedSearch> query);

        /// <summary>
        /// Returns the saved searches matching the given query, evaluated against the supplied context.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <returns>The matching saved searches.</returns>
        IEnumerable<SavedSearch> GetSavedSearches(IQuery<SavedSearch> query, IQueryContext context);

        /// <summary>
        /// Returns the active saved searches owned by the given identity, ordered with the
        /// starred ones first and then by name.
        /// </summary>
        /// <param name="ownerId">The owning identity.</param>
        /// <returns>The owner's saved searches.</returns>
        IReadOnlyList<SavedSearch> GetForOwner(Guid ownerId);

        /// <summary>
        /// Returns the active saved searches owned by the given identity, ordered by most
        /// recently used first and limited to <paramref name="count"/> entries.
        /// </summary>
        /// <param name="ownerId">The owning identity.</param>
        /// <param name="count">The maximum number of entries to return.</param>
        /// <returns>The owner's most recently used saved searches.</returns>
        IReadOnlyList<SavedSearch> GetRecent(Guid ownerId, int count);

        /// <summary>
        /// Returns the active saved searches other identities shared with the given one - those
        /// whose grants carry <c>SavedSearchReadPermission</c> for it - ordered by name. The
        /// identity's own saved searches are not among them.
        /// </summary>
        /// <param name="identityId">The identity the searches are shared with.</param>
        /// <returns>The shared saved searches; empty for nobody.</returns>
        IReadOnlyList<SavedSearch> GetSharedWith(Guid identityId);

        /// <summary>
        /// Determines whether an identity may act on a saved search.
        /// </summary>
        /// <remarks>
        /// The owner may do everything. Anybody else needs a grant on the saved search that
        /// carries the permission: a saved search nobody shared is private, which is the
        /// opposite reading of <c>IPermissionManager.IsGranted</c>, where an unadministered
        /// resource is open. A deleted saved search is refused to everybody.
        /// </remarks>
        /// <param name="savedSearch">The saved search.</param>
        /// <param name="identityId">The identity acting; <see cref="Guid.Empty"/> is nobody and is refused.</param>
        /// <param name="permission">The saved-search permission the action needs.</param>
        /// <returns><see langword="true"/> when the action may proceed.</returns>
        bool IsGranted(SavedSearch savedSearch, Guid identityId, Type permission);

        /// <summary>
        /// Stores the column layout the results table shows a saved search with. Quiet like
        /// <see cref="SetStarred"/>: it fires on every column the reader moves, hides or resizes,
        /// so it raises <see cref="SavedSearchUpdated"/> but no notification.
        /// </summary>
        /// <param name="savedSearchId">The saved search.</param>
        /// <param name="columns">The layout (<c>WebRestApi.TableLayout</c>), or null to clear it.</param>
        /// <returns>The saved search, or <see langword="null"/> when none exists.</returns>
        SavedSearch SetColumns(Guid savedSearchId, string columns);

        /// <summary>
        /// Adds a saved search and raises <see cref="SavedSearchAdded"/>.
        /// </summary>
        /// <param name="savedSearch">The saved search to add. Cannot be null.</param>
        /// <returns>The current instance for chaining.</returns>
        ISavedSearchManager Add(SavedSearch savedSearch);

        /// <summary>
        /// Updates a saved search and raises <see cref="SavedSearchUpdated"/>.
        /// </summary>
        /// <param name="savedSearch">The saved search to update. Cannot be null.</param>
        /// <returns>The current instance for chaining.</returns>
        ISavedSearchManager Update(SavedSearch savedSearch);

        /// <summary>
        /// Soft-deletes the saved search with the given id by setting its
        /// <see cref="SavedSearch.State"/> to <see cref="SavedSearchState.Deleted"/> (every
        /// read path filters on <see cref="SavedSearchState.Active"/>, so the row stops being
        /// surfaced) and raises <see cref="SavedSearchRemoved"/>. No-op when none exists.
        /// </summary>
        /// <param name="savedSearchId">The id of the saved search to remove.</param>
        /// <returns>The current instance for chaining.</returns>
        ISavedSearchManager Remove(Guid savedSearchId);

        /// <summary>
        /// Stamps the saved search as just used (updates <see cref="SavedSearch.LastUsed"/>).
        /// This is a quiet mutation — it does not raise a user-facing notification because it
        /// fires on every run.
        /// </summary>
        /// <param name="savedSearchId">The id of the saved search that was run.</param>
        /// <returns>The updated saved search, or <see langword="null"/> when unknown.</returns>
        SavedSearch RecordUse(Guid savedSearchId);

        /// <summary>
        /// Sets the starred flag of a saved search. This is a quiet mutation — it does not
        /// raise a user-facing notification.
        /// </summary>
        /// <param name="savedSearchId">The id of the saved search.</param>
        /// <param name="starred">Whether the saved search should be starred.</param>
        /// <returns>The updated saved search, or <see langword="null"/> when unknown.</returns>
        SavedSearch SetStarred(Guid savedSearchId, bool starred);
    }
}
