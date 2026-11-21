using KleeneStar.Core.WebParameter;
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
    /// Manages the lifecycle of <see cref="SlaPolicy"/> entities and their dependent targets,
    /// scope rules, and escalation levels.
    /// </summary>
    public sealed class SlaManager : ISlaManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised when a new SLA policy has been added to the manager.
        /// </summary>
        public event EventHandler<SlaPolicy> SlaAdded;

        /// <summary>
        /// Raised when an SLA policy's scalar properties or child collections
        /// (targets, scope rules, escalations) have been updated.
        /// </summary>
        public event EventHandler<SlaPolicy> SlaUpdated;

        /// <summary>
        /// Raised when an SLA policy has been removed from the manager. The event fires
        /// after the underlying cascade has cleaned up the dependent targets, scope rules,
        /// and escalation levels.
        /// </summary>
        public event EventHandler<SlaPolicy> SlaRemoved;

        /// <summary>
        /// Returns the path-segment names reserved by the SLA router and unavailable as
        /// custom policy ids.
        /// </summary>
        public static IEnumerable<string> ReservedSlaNames =>
        [
            "default", "admin", "system", "assets", "api", "add", "edit",
            "delete", "clone", "settings", "icons"
        ];

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private SlaManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the SLA policy identified by the supplied id, including its targets,
        /// scope rules, escalation levels, class, owner, and referenced calendar.
        /// </summary>
        /// <param name="slaId">The policy id.</param>
        /// <returns>The policy, or <c>null</c> when no entry matches.</returns>
        public SlaPolicy GetSla(Guid slaId)
        {
            var query = new Query<SlaPolicy>()
                .Where(x => x.Id == slaId)
                .WithPaging(0, 1);

            return ModelHub.GetSlaPolicies(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the SLA policy identified by the supplied URL-bound id parameter.
        /// </summary>
        /// <param name="slaId">The id parameter parsed from the URL path.</param>
        /// <returns>The policy, or <c>null</c> when no entry matches.</returns>
        public SlaPolicy GetSla(SlaIdParameter slaId)
        {
            ArgumentNullException.ThrowIfNull(slaId);

            var guid = Guid.TryParse(slaId.Value, out var id) ? id : Guid.Empty;

            return GetSla(guid);
        }

        /// <summary>
        /// Returns every SLA policy attached to the class addressed by the supplied
        /// URL-bound class-id parameter.
        /// </summary>
        /// <param name="classId">The class-id parameter parsed from the URL path.</param>
        /// <returns>The policies belonging to the class. The collection may be empty.</returns>
        public IEnumerable<SlaPolicy> GetSlas(ClassIdParameter classId)
        {
            ArgumentNullException.ThrowIfNull(classId);

            var guid = Guid.TryParse(classId.Value, out var id) ? id : Guid.Empty;

            return GetSlas(guid);
        }

        /// <summary>
        /// Returns every SLA policy attached to the class with the supplied id.
        /// </summary>
        /// <param name="classId">The class id.</param>
        /// <returns>The policies belonging to the class. The collection may be empty.</returns>
        public IEnumerable<SlaPolicy> GetSlas(Guid classId)
        {
            var query = new Query<SlaPolicy>()
                .WhereEquals(x => x.ClassId, classId);

            return ModelHub.GetSlaPolicies(query);
        }

        /// <summary>
        /// Returns the SLA policies that satisfy the supplied query. The manager opens
        /// its own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching policies.</returns>
        public IEnumerable<SlaPolicy> GetSlas(IQuery<SlaPolicy> query)
        {
            return ModelHub.GetSlaPolicies(query);
        }

        /// <summary>
        /// Returns the SLA policies that satisfy the supplied query, executed inside
        /// the supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching policies.</returns>
        public IEnumerable<SlaPolicy> GetSlas(IQuery<SlaPolicy> query, IQueryContext context)
        {
            return ModelHub.GetSlaPolicies(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds the supplied policy to the database (together with its targets, scope
        /// rules, and escalation levels), raises <see cref="SlaAdded"/>, and emits a UI
        /// notification. Returns the manager instance to allow chaining.
        /// </summary>
        /// <param name="policy">The policy to add.</param>
        /// <returns>The current manager instance.</returns>
        public ISlaManager Add(SlaPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            ModelHub.Add(policy);

            SlaAdded?.Invoke(this, policy);

            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.sla.created", policy);

            return this;
        }

        /// <summary>
        /// Persists the supplied policy's scalar properties and replaces its targets,
        /// scope rules, and escalation levels with the entries on the incoming entity.
        /// Raises <see cref="SlaUpdated"/> and emits a UI notification.
        /// </summary>
        /// <param name="policy">The policy to update.</param>
        /// <returns>The current manager instance.</returns>
        public ISlaManager Update(SlaPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            ModelHub.Update(policy);

            SlaUpdated?.Invoke(this, policy);

            TryAddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.sla.updated", policy);

            return this;
        }

        // Wraps CoreHub.AddNotification so that callers running outside a fully wired
        // WebExpress host (in particular unit tests using the in-memory fixture) do not
        // crash on the unavailable global ComponentHub.
        private static void TryAddNotification(string titleKey, string messageKey, object subject)
        {
            try
            {
                CoreHub.AddNotification(titleKey, messageKey, subject);
            }
            catch
            {
                // notification is best-effort; ignore failures from incomplete host state
            }
        }

        /// <summary>
        /// Removes the SLA policy identified by the supplied id, cascading the deletion
        /// to its targets, scope rules, and escalation levels. Raises
        /// <see cref="SlaRemoved"/>. No-op when no policy matches the id.
        /// </summary>
        /// <param name="slaId">The id of the policy to remove.</param>
        /// <returns>The current manager instance.</returns>
        public ISlaManager Remove(Guid slaId)
        {
            var existing = GetSla(slaId);

            if (existing is not null)
            {
                ModelHub.Remove(existing);
                SlaRemoved?.Invoke(this, existing);
            }

            return this;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
