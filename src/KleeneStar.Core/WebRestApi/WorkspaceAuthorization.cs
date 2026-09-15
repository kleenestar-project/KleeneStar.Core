using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers whether a request may change a workspace, or administer who may.
    /// </summary>
    /// <remarks>
    /// The workspace dialogs (<c>WWW/Workspaces/{workspacekey}/Edit</c>, <c>Delete</c>,
    /// <c>Clone</c>, <c>Permissions</c>) demand these grants before they render their form, but
    /// a dialog is only one caller of <c>/api/1/workspaces</c>: a form submitted from a page
    /// that was refused, a hand-written client, a script. The endpoint therefore asks the same
    /// question itself, with the same chain and the same semantics as
    /// <see cref="PageAuthorization"/> - an unadministered workspace is open, one grant on it
    /// makes it enforced - and answers a refusal as <c>403</c> rather than by redirecting,
    /// because a REST caller has no page to be sent to.
    /// <para>
    /// A workspace that does not resolve is not an authorization question: the endpoint answers
    /// it as not found, and refusing here would turn a wrong id into a permission error.
    /// Creating a workspace is not guarded either - the chain starts at the workspace, and a
    /// workspace that does not exist yet is on no chain. That is the same gap the pages have
    /// (<c>Workspaces/Add</c> cannot be guarded by this model), stated once rather than hidden.
    /// </para>
    /// </remarks>
    internal static class WorkspaceAuthorization
    {
        /// <summary>
        /// Determines whether the caller may change a workspace.
        /// </summary>
        /// <param name="workspace">The workspace addressed, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayUpdate(Workspace workspace, IRequest request)
        {
            return Check(workspace, request, typeof(WorkspaceUpdatePermission));
        }

        /// <summary>
        /// Determines whether the caller may delete a workspace.
        /// </summary>
        /// <param name="workspace">The workspace addressed, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the deletion may proceed.</returns>
        public static bool MayDelete(Workspace workspace, IRequest request)
        {
            return Check(workspace, request, typeof(WorkspaceDeletePermission));
        }

        /// <summary>
        /// Determines whether the caller may clone a workspace. The grant is read off the
        /// original: the copy does not exist yet and is on no chain.
        /// </summary>
        /// <param name="workspace">The workspace being copied, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the copy may be made.</returns>
        public static bool MayClone(Workspace workspace, IRequest request)
        {
            return Check(workspace, request, typeof(WorkspaceClonePermission));
        }

        /// <summary>
        /// Determines whether the caller may administer who may do what in a workspace.
        /// </summary>
        /// <param name="workspace">The workspace addressed, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the grants may be read and changed.</returns>
        public static bool MayAdminister(Workspace workspace, IRequest request)
        {
            return Check(workspace, request, typeof(WorkspaceManageProfilesPermission));
        }

        /// <summary>
        /// Reads the workspace the <c>id</c> query parameter of a CRUD request addresses.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The workspace, or <see langword="null"/> when the parameter is absent,
        /// malformed or names no workspace.</returns>
        public static Workspace ResolveById(IRequest request)
        {
            return Guid.TryParse(request?.GetParameter<ParameterId>()?.Value, out var id) && id != Guid.Empty
                ? CoreHub.WorkspaceManager.GetWorkspace(id)
                : null;
        }

        /// <summary>
        /// Evaluates a permission on the chain of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <param name="permission">The permission required.</param>
        /// <returns><see langword="true"/> when the caller holds the permission, or when there
        /// is no workspace to hold it on.</returns>
        private static bool Check(Workspace workspace, IRequest request, Type permission)
        {
            if (workspace is null)
            {
                return true;
            }

            return PageAuthorization.IsGranted(request, permission, PageAuthorization.ChainOf(workspace));
        }
    }
}
