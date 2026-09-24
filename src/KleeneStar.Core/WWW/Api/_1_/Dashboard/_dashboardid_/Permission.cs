using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Dashboard._dashboardid_
{
    /// <summary>
    /// Serves the permission dialog of a dashboard: which group holds which policy on it.
    /// </summary>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers.
        /// </summary>
        protected override string Scope => PermissionScope.Dashboard;

        /// <summary>
        /// Determines whether the caller may administer who may do what with the dashboard.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected override bool Authorized(IRequest request)
        {
            var id = request?.GetParameter<DashboardIdParameter>()?.Value;

            return ContentAuthorization.MayUseDashboard(
                Guid.TryParse(id, out var dashboardId) ? dashboardId : null,
                request,
                typeof(WebPermissions.DashboardManageProfilesPermission));
        }

        /// <summary>
        /// Returns the dashboard the request addresses.
        /// </summary>
        /// <param name="request">The request whose route names the dashboard.</param>
        /// <returns>The dashboard id, or null when the route addresses none.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            var id = request?.GetParameter<DashboardIdParameter>()?.Value;

            return Guid.TryParse(id, out var dashboardId)
                ? CoreHub.DashboardManager.GetDashboard(dashboardId)?.Id.ToString()
                : null;
        }
    }
}
