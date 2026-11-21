using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the security levels of the classes and answers who is cleared to see an object
    /// carrying one. See <see cref="ISecurityLevelManager"/> for the rule.
    /// </summary>
    public sealed class SecurityLevelManager : ISecurityLevelManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// The depth of the unrestricted scopes open on the current logical call. Ambient
        /// rather than passed along, because the reads it covers sit several frames below the
        /// caller that knows the read is the system's own.
        /// </summary>
        private static readonly AsyncLocal<int> _unrestricted = new();

        /// <summary>
        /// An event that fires when a security level is added.
        /// </summary>
        public event EventHandler<SecurityLevel> SecurityLevelAdded;

        /// <summary>
        /// An event that fires when a security level is updated.
        /// </summary>
        public event EventHandler<SecurityLevel> SecurityLevelUpdated;

        /// <summary>
        /// An event that fires when a security level is removed.
        /// </summary>
        public event EventHandler<SecurityLevel> SecurityLevelRemoved;

        /// <summary>
        /// Gets a value indicating whether an unrestricted scope is currently open.
        /// </summary>
        public bool IsUnrestricted => _unrestricted.Value > 0;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SecurityLevelManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a security level based on its id.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level.</param>
        /// <returns>The security level, or <c>null</c> when it does not exist.</returns>
        public SecurityLevel GetSecurityLevel(Guid securityLevelId)
        {
            if (securityLevelId == Guid.Empty)
            {
                return null;
            }

            var query = new Query<SecurityLevel>()
                .Where(x => x.Id == securityLevelId)
                .WithPaging(0, 1);

            return ModelHub.GetSecurityLevels(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a security level based on its id.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level.</param>
        /// <returns>The security level, or <c>null</c> when it does not exist.</returns>
        public SecurityLevel GetSecurityLevel(SecurityLevelIdParameter securityLevelId)
        {
            var guid = Guid.TryParse(securityLevelId?.Value, out var id) ? id : Guid.Empty;

            return GetSecurityLevel(guid);
        }

        /// <summary>
        /// Retrieves the security levels defined on a class, ordered by rank.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The security levels of the class, which may be empty.</returns>
        public IEnumerable<SecurityLevel> GetSecurityLevels(ClassIdParameter classId)
        {
            var guid = Guid.TryParse(classId?.Value, out var id) ? id : Guid.Empty;
            var query = new Query<SecurityLevel>()
                .WhereEquals(x => x.ClassId, guid);

            return [.. ModelHub.GetSecurityLevels(query)
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Retrieves the security levels that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <returns>The matching security levels, which may be empty.</returns>
        public IEnumerable<SecurityLevel> GetSecurityLevels(IQuery<SecurityLevel> query)
        {
            return ModelHub.GetSecurityLevels(query);
        }

        /// <summary>
        /// Retrieves the security levels that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <returns>The matching security levels, which may be empty.</returns>
        public IEnumerable<SecurityLevel> GetSecurityLevels(IQuery<SecurityLevel> query, IQueryContext context)
        {
            return ModelHub.GetSecurityLevels(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Returns the level an object of the class starts on.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>The default security level, or <c>null</c>.</returns>
        public SecurityLevel GetDefaultSecurityLevel(Guid classId)
        {
            return GetSecurityLevels(new ClassIdParameter(classId))
                .Where(x => x.State == SecurityLevelState.Active && x.IsDefault)
                .OrderBy(x => x.Rank)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the active levels of a class the supplied identity is cleared for.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <param name="identityId">The identity the levels are offered to.</param>
        /// <returns>The assignable security levels, which may be empty.</returns>
        public IReadOnlyList<SecurityLevel> GetAssignableSecurityLevels(Guid classId, Guid identityId)
        {
            var groups = GetGroupIds(identityId);

            return [.. GetSecurityLevels(new ClassIdParameter(classId))
                .Where(x => x.State == SecurityLevelState.Active && Clears(x, groups))];
        }

        /// <summary>
        /// Determines whether an identity is cleared for a classification.
        /// </summary>
        /// <param name="identityId">The identity asking to see the object.</param>
        /// <param name="securityLevelId">The level the object carries, or <c>null</c>.</param>
        /// <returns><see langword="true"/> when the object may be shown.</returns>
        public bool IsCleared(Guid identityId, Guid? securityLevelId)
        {
            // an unclassified object was never restricted, and the system's own reads see
            // everything regardless of who triggered them
            if (!securityLevelId.HasValue || securityLevelId.Value == Guid.Empty || IsUnrestricted)
            {
                return true;
            }

            var level = GetSecurityLevel(securityLevelId.Value);

            // a classification whose level is gone names a clearance nobody holds. It is the
            // safe reading, and removing a level clears the objects that carried it, so the
            // case only arises when a row was deleted behind the manager's back
            return level is not null && Clears(level, GetGroupIds(identityId));
        }

        /// <summary>
        /// Returns the ids of the levels the supplied identity is cleared for.
        /// </summary>
        /// <param name="identityId">The identity.</param>
        /// <returns>The cleared level ids, which may be empty.</returns>
        public IReadOnlyCollection<Guid> GetClearedSecurityLevelIds(Guid identityId)
        {
            var groups = GetGroupIds(identityId);

            return [.. ModelHub.GetSecurityLevels(new Query<SecurityLevel>())
                .Where(x => Clears(x, groups))
                .Select(x => x.Id)];
        }

        /// <summary>
        /// Narrows an object query to what the supplied identity is cleared to see.
        /// </summary>
        /// <param name="query">The query to narrow. Must not be null.</param>
        /// <param name="identityId">The identity the query is run for.</param>
        /// <returns>The narrowed query.</returns>
        public IQuery<Model.Entities.Object> Restrict(IQuery<Model.Entities.Object> query, Guid identityId)
        {
            if (query is null || IsUnrestricted)
            {
                return query;
            }

            var cleared = GetClearedSecurityLevelIds(identityId);

            // the predicate is expressed over the level ids rather than over the groups,
            // because the clearance itself lives in a serialized column no store can filter on.
            // An identity cleared for nothing still sees every unclassified record, which is
            // why the null branch is not folded away when the list is empty
            if (cleared.Count == 0)
            {
                return query.Where(x => x.SecurityLevelId == null);
            }

            var ids = cleared.ToList();

            return query.Where(x => x.SecurityLevelId == null || ids.Contains(x.SecurityLevelId.Value));
        }

        /// <summary>
        /// Suspends the classification filter for the duration of the scope.
        /// </summary>
        /// <returns>The scope. Disposing it restores the filter.</returns>
        public IDisposable BeginUnrestricted()
        {
            _unrestricted.Value++;

            return new UnrestrictedScope();
        }

        /// <summary>
        /// Adds a security level to the manager.
        /// </summary>
        /// <param name="securityLevelEntity">The security level to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ISecurityLevelManager Add(SecurityLevel securityLevelEntity)
        {
            ArgumentNullException.ThrowIfNull(securityLevelEntity);

            ModelHub.Add(securityLevelEntity);

            DemoteRivalDefaults(securityLevelEntity);

            SecurityLevelAdded?.Invoke(this, securityLevelEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.securitylevel.created", securityLevelEntity);

            return this;
        }

        /// <summary>
        /// Updates a security level in the manager.
        /// </summary>
        /// <param name="securityLevelEntity">The security level to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ISecurityLevelManager Update(SecurityLevel securityLevelEntity)
        {
            ArgumentNullException.ThrowIfNull(securityLevelEntity);

            ModelHub.Update(securityLevelEntity);

            DemoteRivalDefaults(securityLevelEntity);

            SecurityLevelUpdated?.Invoke(this, securityLevelEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.securitylevel.updated", securityLevelEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified security level, declassifying every object that carried it.
        /// </summary>
        /// <param name="securityLevelId">The id of the security level to remove.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ISecurityLevelManager Remove(Guid securityLevelId)
        {
            var securityLevelEntry = GetSecurityLevel(securityLevelId);

            if (securityLevelEntry is not null)
            {
                ModelHub.Remove(securityLevelEntry);
                SecurityLevelRemoved?.Invoke(this, securityLevelEntry);
            }

            return this;
        }

        /// <summary>
        /// Determines whether a level clears an identity holding the supplied groups.
        /// </summary>
        /// <param name="level">The level. Never null.</param>
        /// <param name="groups">The groups the identity belongs to.</param>
        /// <returns><see langword="true"/> when one of the groups is named by the level.</returns>
        private static bool Clears(SecurityLevel level, IReadOnlySet<Guid> groups)
        {
            var permitted = level.PermittedGroupIds;

            // a level that names no group is closed. It is the reading that makes the feature
            // trustworthy: an administrator who creates a level and has not yet said who may
            // see it has restricted the records, not left them open
            if (permitted is null || permitted.Count == 0 || groups.Count == 0)
            {
                return false;
            }

            return permitted.Any(groups.Contains);
        }

        /// <summary>
        /// Returns the ids of the groups the supplied identity belongs to.
        /// </summary>
        /// <param name="identityId">The identity, or <see cref="Guid.Empty"/> for none.</param>
        /// <returns>The group ids, which may be empty.</returns>
        private static IReadOnlySet<Guid> GetGroupIds(Guid identityId)
        {
            if (identityId == Guid.Empty)
            {
                return new HashSet<Guid>();
            }

            var identity = CoreHub.IdentityManager?.GetIdentity(identityId);

            return (identity?.GroupMemberships ?? [])
                .Select(x => x.Group?.Id)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToHashSet();
        }

        /// <summary>
        /// Clears the default flag on the other levels of the same class, so a class always
        /// starts its objects on exactly one level.
        /// </summary>
        /// <param name="securityLevelEntity">The level that was just written.</param>
        private static void DemoteRivalDefaults(SecurityLevel securityLevelEntity)
        {
            if (!securityLevelEntity.IsDefault)
            {
                return;
            }

            var query = new Query<SecurityLevel>()
                .WhereEquals(x => x.ClassId, securityLevelEntity.ClassId);

            foreach (var rival in ModelHub.GetSecurityLevels(query)
                .Where(x => x.IsDefault && x.Id != securityLevelEntity.Id))
            {
                rival.IsDefault = false;

                ModelHub.Update(rival);
            }
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The scope handed out by <see cref="BeginUnrestricted"/>. Closing it decrements the
        /// ambient depth once, however often it is disposed.
        /// </summary>
        private sealed class UnrestrictedScope : IDisposable
        {
            private bool _closed;

            /// <summary>
            /// Restores the classification filter of the enclosing scope.
            /// </summary>
            public void Dispose()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;

                _unrestricted.Value = Math.Max(0, _unrestricted.Value - 1);
            }
        }
    }
}
