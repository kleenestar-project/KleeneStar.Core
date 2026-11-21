using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insight._insightid_
{
    /// <summary>
    /// Serves the permission dialog of an insight: which group holds which policy on it.
    /// </summary>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers.
        /// </summary>
        protected override string Scope => PermissionScope.Insight;

        /// <summary>
        /// Determines whether the caller may administer who may do what with the insight.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected override bool Authorized(IRequest request)
        {
            var id = request?.GetParameter<InsightIdParameter>()?.Value;

            return ContentAuthorization.MayUseInsight(
                Guid.TryParse(id, out var insightId) ? insightId : null,
                request,
                typeof(WebPermissions.InsightManageProfilesPermission));
        }

        /// <summary>
        /// Returns the insight the request addresses.
        /// </summary>
        /// <param name="request">The request whose route names the insight.</param>
        /// <returns>The insight id, or null when the route addresses none.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            var id = request?.GetParameter<InsightIdParameter>()?.Value;

            return Guid.TryParse(id, out var insightId)
                ? CoreHub.InsightManager.GetInsight(insightId)?.Id.ToString()
                : null;
        }
    }
}
