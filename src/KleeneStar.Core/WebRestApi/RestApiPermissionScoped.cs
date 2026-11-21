using KleeneStar.Core.WebPermission;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Serves the permission dialog of one kind of resource: the grants on it, and the additions
    /// and withdrawals the dialog performs.
    /// </summary>
    /// <remarks>
    /// Every resource administers the same thing — which group holds which policy on it — so the
    /// work is done once here and a resource contributes only its scope and how its id is read
    /// from the route.
    ///
    /// The store stays pair-based: a single grant is added and withdrawn here, while the dialog
    /// edits a group's whole policy set at once. Turning that set into the pairs to add and the
    /// pairs to withdraw is the base endpoint's job, so nothing of it appears in this class.
    /// </remarks>
    public abstract class RestApiPermissionScoped : RestApiPermission
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers, as named in
        /// <see cref="PermissionScope"/>.
        /// </summary>
        protected abstract string Scope { get; }

        /// <summary>
        /// Returns the identifier of the resource the request addresses.
        /// </summary>
        /// <param name="request">The request whose route names the resource.</param>
        /// <returns>The identifier, or null when the route addresses none.</returns>
        protected abstract string ResolveScopeId(IRequest request);

        /// <summary>
        /// Determines whether the caller may read and change the grants the request addresses.
        /// </summary>
        /// <remarks>
        /// Administering who may do what on a resource is itself something the resource grants,
        /// and the dialog page that opens this surface demands that grant before it renders.
        /// The endpoint is what the dialog and every other caller submit to, so a resource that
        /// wants the gate to hold answers the same question here; the default refuses nobody,
        /// which is what every scope did before a page demanded anything.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected virtual bool Authorized(IRequest request)
        {
            return true;
        }

        /// <summary>
        /// Answers the grants, once the caller may administer them.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return Authorized(request) ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Adds grants, once the caller may administer them.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return Authorized(request) ? base.Create(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Changes the grants of a group, once the caller may administer them.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        public override IResponse Update(IRequest request)
        {
            return Authorized(request) ? base.Update(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Withdraws a grant, once the caller may administer them.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return Authorized(request) ? base.Delete(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Returns the grants on the addressed resource.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>
        /// The grants, naming the group and the policy. Empty when the route addresses no resource.
        /// </returns>
        protected override IEnumerable<RestApiPermissionItem> RetrieveAssignments(IRequest request)
        {
            var scopeId = ResolveScopeId(request);

            foreach (var assignment in CoreHub.PermissionManager.GetAssignments(Scope, scopeId))
            {
                yield return new RestApiPermissionItem()
                {
                    GroupId = assignment.GroupId.ToString(),
                    GroupName = assignment.Group?.Name,
                    PolicyId = assignment.Policy,
                    PolicyName = PolicyCatalog.GetLabel(assignment.Policy, Scope)
                };
            }
        }

        /// <summary>
        /// Grants a group a policy on the addressed resource.
        /// </summary>
        /// <param name="groupId">The group the policy is granted to.</param>
        /// <param name="policyId">The registered name of the policy.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>
        /// The grant, or null when the resource, the group or the policy is not known — rather than
        /// storing a grant that cannot take effect. A row whose every policy is refused this way
        /// and that holds nothing already is reported as not found by the base endpoint.
        /// </returns>
        protected override RestApiPermissionItem AddAssignment(string groupId, string policyId, IRequest request)
        {
            var scopeId = ResolveScopeId(request);

            if (!Guid.TryParse(groupId, out var group))
            {
                return null;
            }

            var assignment = CoreHub.PermissionManager.Assign(Scope, scopeId, group, policyId);

            if (assignment is null)
            {
                return null;
            }

            return new RestApiPermissionItem()
            {
                GroupId = assignment.GroupId.ToString(),
                GroupName = assignment.Group?.Name ?? CoreHub.GroupManager.GetGroup(group)?.Name,
                PolicyId = assignment.Policy,
                PolicyName = PolicyCatalog.GetLabel(assignment.Policy, Scope)
            };
        }

        /// <summary>
        /// Withdraws a policy from a group on the addressed resource.
        /// </summary>
        /// <param name="groupId">The group the policy is withdrawn from.</param>
        /// <param name="policyId">The registered name of the policy.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>True when a grant was withdrawn; false when there was none.</returns>
        protected override bool RemoveAssignment(string groupId, string policyId, IRequest request)
        {
            var scopeId = ResolveScopeId(request);

            return Guid.TryParse(groupId, out var group) &&
                CoreHub.PermissionManager.Revoke(Scope, scopeId, group, policyId);
        }

        /// <summary>
        /// Narrows the listed rows by the dialog's search term.
        /// </summary>
        /// <remarks>
        /// A row is a group with every policy it holds, so the term is matched against what that
        /// row shows — the group and the chips as labelled — and searching for what is on screen
        /// finds it. The registered policy name is matched as well, because it is what the grant is
        /// stored under and what an administrator is likely to have at hand.
        /// </remarks>
        /// <param name="search">The search term.</param>
        /// <param name="entries">The rows to narrow.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The matching rows.</returns>
        protected override IEnumerable<RestApiPermissionEntry> Filter(string search, IEnumerable<RestApiPermissionEntry> entries, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(search) || search == "null")
            {
                return entries;
            }

            return entries.Where(x =>
                (x.GroupName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.PolicyIds ?? []).Any(policy =>
                    (policy ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (PolicyCatalog.GetLabel(policy, Scope) ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
