using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Answers the two questions the objects sidebar asks about a kind in the workspace of
    /// the current request: whether the kind is configured there at all, and how many
    /// objects of it there are.
    /// </summary>
    /// <remarks>
    /// A kind exists in a workspace exactly when a class of that kind does. The seeded
    /// workspaces show why the distinction matters: every one of them carries the asset tab
    /// set, but only the configuration database has a class of kind asset — so everywhere
    /// else the asset overview is empty by construction, and a link leading to it promises
    /// something the workspace does not have.
    ///
    /// The question is asked per request, not cached, because a class can be added or
    /// retired while the page is open and a stale answer would either hide a kind that now
    /// exists or offer one that no longer does.
    /// </remarks>
    internal static class ObjectKindScope
    {
        /// <summary>
        /// Determines whether the workspace addressed by the request has at least one
        /// active class of the kind.
        /// </summary>
        /// <param name="request">The request the question is asked for.</param>
        /// <param name="kind">The kind key.</param>
        /// <returns>
        /// True when the kind is configured in the workspace. A route that addresses no
        /// existing workspace answers false, so the link is hidden rather than pointing
        /// nowhere.
        /// </returns>
        public static bool IsConfigured(IRequest request, string kind)
        {
            var workspaceId = ResolveWorkspaceId(request);

            if (workspaceId is null)
            {
                return false;
            }

            var query = new Query<Model.Entities.Class>()
                .WhereEquals(x => x.WorkspaceId, workspaceId.Value)
                .WhereEquals(x => x.Kind, kind);

            return CoreHub.ClassManager
                .GetClasses(query)
                .Any(x => x.State == ClassState.Active);
        }

        /// <summary>
        /// Counts the active objects of the kind in the workspace addressed by the request.
        /// </summary>
        /// <param name="request">The request the question is asked for.</param>
        /// <param name="kind">The kind key.</param>
        /// <returns>The number of objects, or zero when the workspace is unresolvable.</returns>
        public static int Count(IRequest request, string kind)
        {
            var workspaceId = ResolveWorkspaceId(request);

            if (workspaceId is null)
            {
                return 0;
            }

            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId.Value)
                .WhereEquals(x => x.Kind, kind);

            return CoreHub.ObjectManager
                .GetObjects(query)
                .Count(x => x.State == WorkspaceState.Active);
        }

        /// <summary>
        /// Resolves the workspace of the current request: directly from the workspace-key
        /// parameter when present, otherwise through the workspace of the object addressed
        /// by the object-key parameter — the sidebar is rendered on the detail pages too,
        /// whose route names no workspace.
        /// </summary>
        /// <param name="request">The request the question is asked for.</param>
        /// <returns>The workspace id, or null when unresolvable.</returns>
        private static Guid? ResolveWorkspaceId(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            if (!string.IsNullOrWhiteSpace(workspaceKey))
            {
                return CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey)?.Id;
            }

            var objectKey = request?.GetParameter<ObjectKeyParameter>()?.Value;

            return CoreHub.ObjectManager.GetObjectByKey(objectKey)?.WorkspaceId;
        }
    }
}
