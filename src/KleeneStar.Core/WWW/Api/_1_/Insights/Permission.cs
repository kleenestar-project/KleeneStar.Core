using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Serves the permission dialog of the insight overview: which groups may create insights.
    /// </summary>
    /// <remarks>
    /// A new insight is on no resource yet, so the grants that say who may create one sit on
    /// the installation-wide insight resource (<see cref="ContentAuthorization.InsightCreationResource"/>).
    /// While it carries no grant every signed-in caller may create insights; the first grant
    /// narrows creation to the groups it names. Only <c>insight_creator_policy</c> is offered and
    /// accepted here - the other insight policies say something about one insight and would mean
    /// nothing on this resource. Administering it is for the installation's administrators
    /// (<see cref="ContentAuthorization.MayAdministerInsightCreation"/>).
    /// </remarks>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// The one policy the creation resource is granted with.
        /// </summary>
        public const string CreatorPolicy = "insight_creator_policy";

        /// <summary>
        /// Gets the kind of resource the grants are stored against.
        /// </summary>
        protected override string Scope => PermissionScope.Insight;

        /// <summary>
        /// Admits the installation's administrators only.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may administer insight creation.</returns>
        protected override bool Authorized(IRequest request)
        {
            return ContentAuthorization.MayAdministerInsightCreation(request);
        }

        /// <summary>
        /// Resolves the installation-wide insight resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The scope id of the creation resource.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            return ContentAuthorization.InsightCreationScopeId;
        }

        /// <summary>
        /// Grants a group the creator policy; any other policy is refused.
        /// </summary>
        /// <param name="groupId">The group.</param>
        /// <param name="policyId">The policy.</param>
        /// <param name="request">The request.</param>
        /// <returns>The assignment, or <see langword="null"/> when refused.</returns>
        protected override RestApiPermissionItem AddAssignment(string groupId, string policyId, IRequest request)
        {
            return string.Equals(policyId, CreatorPolicy, StringComparison.OrdinalIgnoreCase)
                ? base.AddAssignment(groupId, CreatorPolicy, request)
                : null;
        }
    }
}
