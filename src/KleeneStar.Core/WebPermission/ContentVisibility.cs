using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Narrows the reads of objects and workspaces to what the caller's grants let them read.
    /// </summary>
    /// <remarks>
    /// The permission counterpart of the security-level filter, and enforced the same way: once,
    /// as a <em>predicate on the query</em> of the manager every read passes through
    /// (<c>ObjectManager</c>), so it lands before paging and guards a list written tomorrow
    /// without its author knowing. An object is readable when its class's chain grants
    /// <see cref="ObjectReadPermission"/> (a workspace grant reaches it through
    /// <see cref="PermissionImplication"/>); a workspace appears in a list when its chain grants
    /// <see cref="WorkspaceReadPermission"/>.
    /// <para>
    /// <b>Nobody acting is the system acting.</b> Without a request and without a
    /// <c>BeginIdentity</c> scope (<c>ISessionManager.HasCaller</c>) nothing is narrowed: the
    /// start-up, the seeder and background work read what exists. A read made on the system's
    /// behalf <em>inside</em> a request lifts both filters with the one scope that already
    /// exists for it, <c>ISecurityLevelManager.BeginUnrestricted</c>.
    /// </para>
    /// <para>
    /// The refused sets are computed once per request and caller and dropped when a grant
    /// changes, because a page asks for objects many times and the answer depends on nothing
    /// else.
    /// </para>
    /// </remarks>
    public static class ContentVisibility
    {
        /// <summary>
        /// Bumped on every change of a grant, which invalidates what requests remembered.
        /// </summary>
        private static int _version;

        /// <summary>
        /// The refused sets remembered beside each request.
        /// </summary>
        private static readonly ConditionalWeakTable<IRequest, Memo> _memos = new();

        /// <summary>
        /// Narrows an object query to the objects the caller may read.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query, or the query itself when nothing narrows.</returns>
        public static IQuery<ObjectEntity> Restrict(IQuery<ObjectEntity> query)
        {
            if (query is null || !Applies())
            {
                return query;
            }

            var refused = Refused(Kind.Class, typeof(ObjectReadPermission));

            if (refused.Count == 0)
            {
                return query;
            }

            var ids = refused.ToList();

            return query.Where(x => !ids.Contains(x.ClassId));
        }

        /// <summary>
        /// Narrows a workspace query to the workspaces the caller may read.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query, or the query itself when nothing narrows.</returns>
        public static IQuery<Workspace> Restrict(IQuery<Workspace> query)
        {
            if (query is null || !Applies())
            {
                return query;
            }

            var refused = Refused(Kind.Workspace, typeof(WorkspaceReadPermission));

            if (refused.Count == 0)
            {
                return query;
            }

            var ids = refused.ToList();

            return query.Where(x => !ids.Contains(x.Id));
        }

        /// <summary>
        /// Determines whether the caller may read a workspace - the single-record answer of
        /// <see cref="Restrict(IQuery{Workspace})"/>, for a list that is already materialized.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns><see langword="true"/> when the workspace may be shown.</returns>
        public static bool MayRead(Guid workspaceId)
        {
            return !Applies() || !Refused(Kind.Workspace, typeof(WorkspaceReadPermission)).Contains(workspaceId);
        }

        /// <summary>
        /// Determines whether the caller may read at least one workspace - whether a list of
        /// workspaces, and the menu leading to it, has anything to show them.
        /// </summary>
        /// <returns><see langword="true"/> when one workspace or more is readable.</returns>
        public static bool MayReadAnyWorkspace()
        {
            return CoreHub.WorkspaceManager
                .GetWorkspaces(Restrict(new Query<Workspace>().WithPaging(0, 1)))
                .Any();
        }

        /// <summary>
        /// Determines whether the caller may read the objects of at least one class of a kind -
        /// whether the kind's header menu has anything for them.
        /// </summary>
        /// <param name="kind">The object kind (issue, document, blog, asset, ...).</param>
        /// <returns><see langword="true"/> when a class of the kind is readable.</returns>
        public static bool MayReadAnyOfKind(string kind)
        {
            var normalized = ObjectKind.Normalize(kind);
            var classes = CoreHub.ClassManager
                .GetClasses(new Query<Class>())
                .Where(x => string.Equals(ObjectKind.Normalize(x.Kind), normalized, StringComparison.OrdinalIgnoreCase));

            if (!Applies())
            {
                return classes.Any();
            }

            var refused = Refused(Kind.Class, typeof(ObjectReadPermission));

            return classes.Any(x => !refused.Contains(x.Id));
        }

        /// <summary>
        /// Narrows a dashboard query to the dashboards the caller may read.
        /// </summary>
        /// <remarks>
        /// A dashboard belongs to no workspace, so the workspace grants that keep an anonymous
        /// visitor out say nothing about it. Dashboards are therefore for signed-in callers: an
        /// anonymous one reads none. A signed-in caller reads every dashboard whose own grants
        /// (the dashboard's permission dialog) do not refuse <see cref="DashboardReadPermission"/>.
        /// </remarks>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query.</returns>
        public static IQuery<Dashboard> Restrict(IQuery<Dashboard> query)
        {
            if (query is null || !Applies())
            {
                return query;
            }

            if (CoreHub.SessionManager.GetCurrentIdentityId(null) == Guid.Empty)
            {
                return query.Where(x => false);
            }

            var refused = Refused(Kind.Dashboard, typeof(DashboardReadPermission));

            if (refused.Count == 0)
            {
                return query;
            }

            var ids = refused.ToList();

            return query.Where(x => !ids.Contains(x.Id));
        }

        /// <summary>
        /// Determines whether the caller may read at least one dashboard, or create one - whether
        /// the dashboard menu has anything for them. Both need a signed-in caller.
        /// </summary>
        /// <returns><see langword="true"/> when the dashboard menu is offered.</returns>
        public static bool MayUseDashboards()
        {
            return !Applies() || CoreHub.SessionManager.GetCurrentIdentityId(null) != Guid.Empty;
        }

        /// <summary>
        /// Forgets every remembered answer. Called when a grant is added or withdrawn.
        /// </summary>
        public static void Invalidate()
        {
            Interlocked.Increment(ref _version);
        }

        /// <summary>
        /// Determines whether anything is to be narrowed: somebody is acting, and not on the
        /// system's behalf.
        /// </summary>
        /// <returns><see langword="true"/> when the reads are narrowed.</returns>
        private static bool Applies()
        {
            return CoreHub.PermissionManager is not null
                && CoreHub.SessionManager?.HasCaller == true
                && CoreHub.SecurityLevelManager?.IsUnrestricted != true;
        }

        /// <summary>
        /// Returns the refused set of the current caller, remembered beside the request.
        /// </summary>
        /// <param name="kind">Whether classes or workspaces are asked for.</param>
        /// <param name="permission">The permission required.</param>
        /// <returns>The refused ids.</returns>
        private static IReadOnlySet<Guid> Refused(Kind kind, Type permission)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(null);

            IReadOnlySet<Guid> compute() => kind switch
            {
                Kind.Class => CoreHub.PermissionManager.GetRefusedClassIds(identityId, permission),
                Kind.Dashboard => CoreHub.PermissionManager.GetRefusedIds(PermissionScope.Dashboard, identityId, permission),
                _ => CoreHub.PermissionManager.GetRefusedWorkspaceIds(identityId, permission)
            };

            // a BeginIdentity scope outside a request has nothing to remember beside
            if (WebEx.CurrentRequest is not { } request)
            {
                return compute();
            }

            var memo = _memos.GetValue(request, _ => new Memo());
            var version = Volatile.Read(ref _version);

            if (memo.Version != version)
            {
                memo.Sets.Clear();
                memo.Version = version;
            }

            return memo.Sets.GetOrAdd((kind, identityId, permission), _ => compute());
        }

        /// <summary>
        /// What a refused set is a set of.
        /// </summary>
        private enum Kind
        {
            Class,
            Workspace,
            Dashboard
        }

        /// <summary>
        /// The answers remembered for one request.
        /// </summary>
        private sealed class Memo
        {
            /// <summary>
            /// The grant version the answers were computed at.
            /// </summary>
            public int Version { get; set; } = -1;

            /// <summary>
            /// The refused sets per (kind, caller, permission).
            /// </summary>
            public ConcurrentDictionary<(Kind, Guid, Type), IReadOnlySet<Guid>> Sets { get; } = new();
        }
    }
}
