using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Class._classid_
{
    /// <summary>
    /// Serves the permission dialog of a class: which group holds which policy on it.
    /// </summary>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers.
        /// </summary>
        protected override string Scope => PermissionScope.Class;

        /// <summary>
        /// Returns the class the request addresses.
        /// </summary>
        /// <remarks>
        /// The class is looked up rather than taken from the route as-is, so a grant cannot be
        /// stored against an id that names nothing.
        /// </remarks>
        /// <param name="request">The request whose route names the class.</param>
        /// <returns>The class id, or null when the route addresses none.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            var id = request?.GetParameter<ClassIdParameter>()?.Value;

            return Guid.TryParse(id, out var classId)
                ? CoreHub.ClassManager.GetClass(classId)?.Id.ToString()
                : null;
        }

        /// <summary>
        /// Determines whether the caller may administer who may do what with the class - its
        /// administrators, which includes the administrators of its workspace.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected override bool Authorized(IRequest request)
        {
            var id = request?.GetParameter<ClassIdParameter>()?.Value;
            var @class = Guid.TryParse(id, out var classId) ? CoreHub.ClassManager.GetClass(classId) : null;

            return @class is null
                || PageAuthorization.IsGranted(request, typeof(WebPermissions.ClassManagePermissionsPermission), PageAuthorization.ChainOf(@class));
        }
    }
}
