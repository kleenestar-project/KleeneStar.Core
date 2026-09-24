using KleeneStar.Core.WebPermission;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the group-to-policy grants the permission dialogs administer.
    /// </summary>
    public sealed class PermissionManager : IPermissionManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a grant is added.
        /// </summary>
        public event EventHandler<PermissionAssignment> PermissionAssigned;

        /// <summary>
        /// An event that fires when a grant is withdrawn.
        /// </summary>
        public event EventHandler<PermissionAssignment> PermissionRevoked;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private PermissionManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the grants on one resource, in the order they are listed.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <returns>The grants, ordered by group and then by policy.</returns>
        public IEnumerable<PermissionAssignment> GetAssignments(string scope, string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(scopeId))
            {
                return [];
            }

            var query = new Query<PermissionAssignment>()
                .WhereEquals(x => x.Scope, scope);

            // the resource id is compared after materialization so the comparison is the same one
            // the route makes, rather than whatever collation the store happens to use
            return [.. ModelHub.GetPermissionAssignments(query)
                .Where(x => string.Equals(x.ScopeId, scopeId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Group?.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Policy, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Determines whether an identity holds a permission on a resource.
        /// </summary>
        /// <remarks>
        /// See <see cref="IPermissionManager.IsGranted"/> for the rule an unadministered resource
        /// is answered by. The evaluation is deliberately cheap on the common path: the grants of
        /// the chain are read first, and an empty result ends the check before the identity is
        /// resolved at all.
        /// </remarks>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <param name="resources">The resource and the resources that contain it, most specific first.</param>
        /// <returns><see langword="true"/> when the action may proceed.</returns>
        public bool IsGranted(Guid identityId, Type permission, params PermissionResource[] resources)
        {
            if (permission is null)
            {
                return true;
            }

            var grants = (resources ?? [])
                .Where(x => x.IsResolved)
                .SelectMany(x => GetAssignments(x.Scope, x.ScopeId))
                .ToList();

            return Evaluate(GroupsOf(identityId), permission, grants);
        }

        /// <summary>
        /// Returns the classes on whose chain an identity does <em>not</em> hold a permission.
        /// </summary>
        /// <remarks>
        /// The set answer of <see cref="IsGranted"/> for every class at once, for a caller that
        /// narrows a query rather than judging one record: the object reads exclude the objects
        /// of these classes. All grants are read in one pass and every class is judged in memory
        /// by the same rule, so the two answers cannot disagree. A class on an unadministered
        /// chain is refused only for an administrative permission, and then only to a caller
        /// outside the installation's administrators.
        /// </remarks>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused classes.</returns>
        public IReadOnlySet<Guid> GetRefusedClassIds(Guid identityId, Type permission)
        {
            if (permission is null)
            {
                return new HashSet<Guid>();
            }

            var grants = GetAllAssignments();
            var groups = GroupsOf(identityId);
            var administrative = PermissionImplication.IsAdministrative(permission);
            var refused = new HashSet<Guid>();

            // nothing administered anywhere and nothing demanding an administrator: every class
            // is open, and the class table need not be read at all
            if (grants.Count == 0 && !administrative)
            {
                return refused;
            }

            foreach (var @class in CoreHub.ClassManager.GetClasses(new Query<Class>()))
            {
                var chain = Lookup(grants, PermissionScope.Class, @class.Id.ToString())
                    .Concat(Lookup(grants, PermissionScope.Workspace, @class.WorkspaceId.ToString()))
                    .ToList();

                if (!Evaluate(groups, permission, chain))
                {
                    refused.Add(@class.Id);
                }
            }

            return refused;
        }

        /// <summary>
        /// Returns the workspaces on whose chain an identity does <em>not</em> hold a permission,
        /// for a caller that narrows a list of workspaces.
        /// </summary>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused workspaces.</returns>
        public IReadOnlySet<Guid> GetRefusedWorkspaceIds(Guid identityId, Type permission)
        {
            if (permission is null)
            {
                return new HashSet<Guid>();
            }

            var grants = GetAllAssignments();
            var groups = GroupsOf(identityId);
            var refused = new HashSet<Guid>();

            // an unadministered workspace refuses only an administrative permission, so the
            // workspace table is read only then; otherwise the administered ones are all there is
            var candidates = PermissionImplication.IsAdministrative(permission)
                ? CoreHub.WorkspaceManager.GetWorkspaces(new Query<Workspace>()).Select(x => x.Id)
                : grants.Keys
                    .Where(x => x.Scope == PermissionScope.Workspace)
                    .Select(x => Guid.TryParse(x.ScopeId, out var id) ? id : Guid.Empty)
                    .Where(x => x != Guid.Empty);

            foreach (var workspaceId in candidates.Distinct())
            {
                if (!Evaluate(groups, permission, [.. Lookup(grants, PermissionScope.Workspace, workspaceId.ToString())]))
                {
                    refused.Add(workspaceId);
                }
            }

            return refused;
        }

        /// <summary>
        /// Returns the resources of one scope, each its own whole chain (a dashboard, a
        /// calendar), on which an identity does <em>not</em> hold a non-administrative
        /// permission. Only administered resources can refuse one, so only they are judged.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused resources.</returns>
        public IReadOnlySet<Guid> GetRefusedIds(string scope, Guid identityId, Type permission)
        {
            var refused = new HashSet<Guid>();

            if (permission is null || string.IsNullOrWhiteSpace(scope))
            {
                return refused;
            }

            var grants = GetAllAssignments();
            var groups = GroupsOf(identityId);

            foreach (var (key, chain) in grants.Where(x => x.Key.Scope == scope.ToLowerInvariant()))
            {
                if (Guid.TryParse(key.ScopeId, out var id) && !Evaluate(groups, permission, chain))
                {
                    refused.Add(id);
                }
            }

            return refused;
        }

        /// <summary>
        /// Judges a permission against the grants of one chain.
        /// </summary>
        /// <param name="groups">The groups the caller is a member of, implicit ones included.</param>
        /// <param name="permission">The permission type required.</param>
        /// <param name="grants">The grants anywhere on the chain.</param>
        /// <returns><see langword="true"/> when the action may proceed.</returns>
        private static bool Evaluate(IReadOnlySet<Guid> groups, Type permission, IReadOnlyCollection<PermissionAssignment> grants)
        {
            // nothing on the chain was ever administered, so the installation has expressed no
            // restriction on using it - but administering it is still somebody's job, and until a
            // grant names whose, it is the installation's administrators' (fail closed)
            if (grants.Count == 0)
            {
                return !PermissionImplication.IsAdministrative(permission)
                    || groups.Contains(Group.AdministratorsId);
            }

            // one policy can be granted several times over the chain, and resolving what it
            // carries is the expensive half, so each distinct policy is judged once
            var accepted = PermissionImplication.Satisfying(permission).ToList();

            return grants
                .Where(x => groups.Contains(x.GroupId))
                .Select(x => x.Policy)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(policy => accepted.Any(x => Carries(policy, x)));
        }

        /// <summary>
        /// Returns the groups a caller is a member of, the implicit ones included.
        /// </summary>
        /// <remarks>
        /// Every caller is in <see cref="Group.AnonymousId"/>; a caller that resolves to a stored
        /// account is also in <see cref="Group.AuthenticatedId"/> and in every group its account
        /// joined. The session manager already turns a locked account into
        /// <see cref="Guid.Empty"/>, so an id that resolves here stands for somebody signed in.
        /// </remarks>
        /// <param name="identityId">The identity, <see cref="Guid.Empty"/> for nobody.</param>
        /// <returns>The group ids.</returns>
        private static HashSet<Guid> GroupsOf(Guid identityId)
        {
            var groups = new HashSet<Guid> { Group.AnonymousId };
            var identity = identityId == Guid.Empty ? null : CoreHub.IdentityManager?.GetIdentity(identityId);

            if (identity is null)
            {
                return groups;
            }

            groups.Add(Group.AuthenticatedId);

            foreach (var membership in identity.GroupMemberships ?? [])
            {
                if (membership.Group?.Id is { } groupId)
                {
                    groups.Add(groupId);
                }
            }

            return groups;
        }

        /// <summary>
        /// Reads every grant once, keyed by the resource it sits on.
        /// </summary>
        /// <returns>The grants per (scope, resource id), the id compared ignoring case.</returns>
        private static Dictionary<(string Scope, string ScopeId), List<PermissionAssignment>> GetAllAssignments()
        {
            return ModelHub.GetPermissionAssignments(new Query<PermissionAssignment>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Scope) && !string.IsNullOrWhiteSpace(x.ScopeId))
                .GroupBy(x => (x.Scope.ToLowerInvariant(), x.ScopeId.ToLowerInvariant()))
                .ToDictionary(x => x.Key, x => x.ToList());
        }

        /// <summary>
        /// Returns the grants on one resource out of <see cref="GetAllAssignments"/>.
        /// </summary>
        /// <param name="grants">All grants.</param>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource.</param>
        /// <returns>The grants, empty when there are none.</returns>
        private static IEnumerable<PermissionAssignment> Lookup(Dictionary<(string Scope, string ScopeId), List<PermissionAssignment>> grants, string scope, string scopeId)
        {
            return grants.TryGetValue((scope.ToLowerInvariant(), scopeId.ToLowerInvariant()), out var found) ? found : [];
        }

        /// <summary>
        /// Remembers which policy carries which permission. The registry of policies does not
        /// change while the host runs, and the question is asked for every grant of every chain.
        /// </summary>
        private static readonly ConcurrentDictionary<(string Policy, Type Permission), bool> _carries = new();

        /// <summary>
        /// Determines whether a granted policy carries a permission.
        /// </summary>
        /// <remarks>
        /// The question is put to the framework registry rather than answered by reading the
        /// attributes here, so a policy the application declares and one a plugin contributes are
        /// judged by the same rule. A grant naming a policy the running system no longer knows
        /// carries nothing, which is the safe reading: it was written against a component that is
        /// gone.
        /// </remarks>
        /// <param name="policy">The registered policy name the grant records.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns><see langword="true"/> when the policy includes the permission.</returns>
        private static bool Carries(string policy, Type permission)
        {
            var identityManager = CoreHub.ComponentHub?.IdentityManager;

            // without a registry (a fixture wiring none) there is nothing to remember
            if (identityManager is null)
            {
                return false;
            }

            var key = (policy.ToLowerInvariant(), permission);

            if (_carries.TryGetValue(key, out var carries))
            {
                return carries;
            }

            var policyType = PolicyCatalog.GetPolicyType(policy);

            // an unknown policy is not remembered: a registry still filling up (start-up, a
            // fixture) must not fix a "no" that stops being true a moment later
            if (policyType is null)
            {
                return false;
            }

            carries = identityManager.CheckAccess(CoreHub.ApplicationContext, policyType, permission);
            _carries[key] = carries;

            return carries;
        }

        /// <summary>
        /// Grants a group a policy on a resource.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <param name="groupId">The group the policy is granted to.</param>
        /// <param name="policy">The registered name of the policy.</param>
        /// <returns>The grant, or null when the group or the policy is not known.</returns>
        public PermissionAssignment Assign(string scope, string scopeId, Guid groupId, string policy)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(scopeId))
            {
                return null;
            }

            // a grant naming a policy no guard knows would never take effect, and a grant to a
            // group that does not exist could never be read back with a name to show
            if (!PolicyCatalog.IsKnown(policy, scope) || CoreHub.GroupManager.GetGroup(groupId) is null)
            {
                return null;
            }

            var existing = GetAssignments(scope, scopeId)
                .FirstOrDefault(x => x.GroupId == groupId && string.Equals(x.Policy, policy, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                return existing;
            }

            var assignment = new PermissionAssignment(Guid.NewGuid())
            {
                Scope = scope,
                ScopeId = scopeId,
                GroupId = groupId,
                Policy = policy,
                Created = DateTime.UtcNow
            };

            ModelHub.Add(assignment);

            // the reads narrowed by the grants remembered them per request
            ContentVisibility.Invalidate();

            PermissionAssigned?.Invoke(this, assignment);

            // the stored record carries the group, which the caller needs to name it in the list
            return GetAssignments(scope, scopeId)
                .FirstOrDefault(x => x.Id == assignment.Id) ?? assignment;
        }

        /// <summary>
        /// Withdraws a policy from a group on a resource.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <param name="groupId">The group the policy is withdrawn from.</param>
        /// <param name="policy">The registered name of the policy.</param>
        /// <returns>True when a grant was withdrawn; false when there was none.</returns>
        public bool Revoke(string scope, string scopeId, Guid groupId, string policy)
        {
            var assignment = GetAssignments(scope, scopeId)
                .FirstOrDefault(x => x.GroupId == groupId && string.Equals(x.Policy, policy, StringComparison.OrdinalIgnoreCase));

            if (assignment is null)
            {
                return false;
            }

            ModelHub.Remove(assignment);

            // the reads narrowed by the grants remembered them per request
            ContentVisibility.Invalidate();

            PermissionRevoked?.Invoke(this, assignment);

            return true;
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
