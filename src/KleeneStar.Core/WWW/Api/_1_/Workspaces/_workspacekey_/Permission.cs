using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_
{
    /// <summary>
    /// Serves the permission dialog of a workspace: which group holds which policy on it.
    /// </summary>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers.
        /// </summary>
        protected override string Scope => PermissionScope.Workspace;

        /// <summary>
        /// Returns the workspace the request addresses.
        /// </summary>
        /// <remarks>
        /// The grants are keyed by the workspace's id rather than by the key in the route, so
        /// renaming a workspace does not orphan them.
        /// </remarks>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns>The workspace id, or null when the route addresses none.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(key)?.Id.ToString();
        }

        /// <summary>
        /// Determines whether the caller may administer the grants of the addressed workspace -
        /// the same question the permission dialog page demands an answer to before it opens.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected override bool Authorized(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return WorkspaceAuthorization.MayAdminister(CoreHub.WorkspaceManager.GetWorkspaceByKey(key), request);
        }
    }
}
