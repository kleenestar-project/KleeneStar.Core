using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages per-identity saved searches, including adding, retrieving, updating, and
    /// removing, as well as the quiet "record use" and "set starred" mutations.
    /// </summary>
    /// <remarks>
    /// The class owns the user-facing notifications for create/update/delete; endpoints must
    /// not also notify. The recency and starring mutations are deliberately quiet because
    /// they fire on every search run / toggle.
    /// </remarks>
    public sealed class SavedSearchManager : ISavedSearchManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <inheritdoc/>
        public event EventHandler<SavedSearch> SavedSearchAdded;

        /// <inheritdoc/>
        public event EventHandler<SavedSearch> SavedSearchUpdated;

        /// <inheritdoc/>
        public event EventHandler<SavedSearch> SavedSearchRemoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SavedSearchManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public SavedSearch GetSavedSearch(Guid savedSearchId)
        {
            var query = new Query<SavedSearch>()
                .Where(x => x.Id == savedSearchId)
                .WithPaging(0, 1);

            return ModelHub.GetSavedSearches(query)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public SavedSearch GetSavedSearch(SavedSearchIdParameter savedSearchId)
        {
            var guid = Guid.TryParse(savedSearchId?.Value, out var id) ? id : Guid.Empty;

            return GetSavedSearch(guid);
        }

        /// <inheritdoc/>
        public IEnumerable<SavedSearch> GetSavedSearches(IQuery<SavedSearch> query)
        {
            return ModelHub.GetSavedSearches(query);
        }

        /// <inheritdoc/>
        public IEnumerable<SavedSearch> GetSavedSearches(IQuery<SavedSearch> query, IQueryContext context)
        {
            return ModelHub.GetSavedSearches(query, context as KleeneStarDbContext);
        }

        /// <inheritdoc/>
        public IReadOnlyList<SavedSearch> GetForOwner(Guid ownerId)
        {
            return [.. ModelHub.GetSavedSearches(new Query<SavedSearch>())
                .Where(x => x.OwnerId == ownerId && x.State == SavedSearchState.Active)
                .OrderByDescending(x => x.Starred)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <inheritdoc/>
        public IReadOnlyList<SavedSearch> GetRecent(Guid ownerId, int count)
        {
            return [.. ModelHub.GetSavedSearches(new Query<SavedSearch>())
                .Where(x => x.OwnerId == ownerId && x.State == SavedSearchState.Active)
                .OrderByDescending(x => x.LastUsed)
                .Take(Math.Max(0, count))];
        }

        /// <inheritdoc/>
        public IReadOnlyList<SavedSearch> GetSharedWith(Guid identityId)
        {
            if (identityId == Guid.Empty || CoreHub.PermissionManager is null)
            {
                return [];
            }

            var shared = CoreHub.PermissionManager.GetGrantedIds(PermissionScope.SavedSearch, identityId, typeof(SavedSearchReadPermission));

            if (shared.Count == 0)
            {
                return [];
            }

            var ids = shared.ToList();

            return [.. ModelHub.GetSavedSearches(new Query<SavedSearch>().Where(x => ids.Contains(x.Id)))
                .Where(x => x.OwnerId != identityId && x.State == SavedSearchState.Active)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
        }

        /// <inheritdoc/>
        public bool IsGranted(SavedSearch savedSearch, Guid identityId, Type permission)
        {
            if (savedSearch is null || identityId == Guid.Empty || savedSearch.State != SavedSearchState.Active)
            {
                return false;
            }

            if (savedSearch.OwnerId == identityId)
            {
                return true;
            }

            return CoreHub.PermissionManager?
                .GetGrantedIds(PermissionScope.SavedSearch, identityId, permission)
                .Contains(savedSearch.Id) == true;
        }

        /// <inheritdoc/>
        public ISavedSearchManager Add(SavedSearch savedSearch)
        {
            ArgumentNullException.ThrowIfNull(savedSearch);

            ModelHub.Add(savedSearch);

            SavedSearchAdded?.Invoke(this, savedSearch);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.savedsearch.created", savedSearch);

            return this;
        }

        /// <inheritdoc/>
        public ISavedSearchManager Update(SavedSearch savedSearch)
        {
            ArgumentNullException.ThrowIfNull(savedSearch);

            savedSearch.Updated = DateTime.UtcNow;
            ModelHub.Update(savedSearch);

            SavedSearchUpdated?.Invoke(this, savedSearch);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.savedsearch.updated", savedSearch);

            return this;
        }

        /// <inheritdoc/>
        public ISavedSearchManager Remove(Guid savedSearchId)
        {
            var savedSearch = GetSavedSearch(savedSearchId);

            if (savedSearch is not null)
            {
                // Soft-delete: flip the state to Deleted (every read path filters on Active)
                // rather than hard-removing the row, mirroring the comment soft-delete.
                savedSearch.State = SavedSearchState.Deleted;
                savedSearch.Updated = DateTime.UtcNow;
                ModelHub.Update(savedSearch);
                SavedSearchRemoved?.Invoke(this, savedSearch);

                CoreHub.AddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.savedsearch.deleted", savedSearch);
            }

            return this;
        }

        /// <inheritdoc/>
        public SavedSearch RecordUse(Guid savedSearchId)
        {
            var savedSearch = GetSavedSearch(savedSearchId);

            if (savedSearch is null)
            {
                return null;
            }

            savedSearch.LastUsed = DateTime.UtcNow;
            ModelHub.Update(savedSearch);

            SavedSearchUpdated?.Invoke(this, savedSearch);

            return savedSearch;
        }

        /// <inheritdoc/>
        public SavedSearch SetStarred(Guid savedSearchId, bool starred)
        {
            var savedSearch = GetSavedSearch(savedSearchId);

            if (savedSearch is null)
            {
                return null;
            }

            savedSearch.Starred = starred;
            savedSearch.Updated = DateTime.UtcNow;
            ModelHub.Update(savedSearch);

            SavedSearchUpdated?.Invoke(this, savedSearch);

            return savedSearch;
        }

        /// <inheritdoc/>
        public SavedSearch SetColumns(Guid savedSearchId, string columns)
        {
            var savedSearch = GetSavedSearch(savedSearchId);

            if (savedSearch is null)
            {
                return null;
            }

            savedSearch.Columns = string.IsNullOrWhiteSpace(columns) ? null : columns;
            savedSearch.Updated = DateTime.UtcNow;
            ModelHub.Update(savedSearch);

            SavedSearchUpdated?.Invoke(this, savedSearch);

            return savedSearch;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
