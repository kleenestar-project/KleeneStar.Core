using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the group-to-policy grants the permission dialogs
    /// administer.
    /// </summary>
    public interface IPermissionManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a grant is added.
        /// </summary>
        event EventHandler<PermissionAssignment> PermissionAssigned;

        /// <summary>
        /// An event that fires when a grant is withdrawn.
        /// </summary>
        event EventHandler<PermissionAssignment> PermissionRevoked;

        /// <summary>
        /// Returns the grants on one resource, in the order they are listed.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <returns>The grants, ordered by group and then by policy.</returns>
        IEnumerable<PermissionAssignment> GetAssignments(string scope, string scopeId);

        /// <summary>
        /// Determines whether an identity holds a permission on a resource.
        /// </summary>
        /// <remarks>
        /// The check walks the chain the caller states, from the record itself outwards, and asks
        /// three questions in turn: which groups the identity belongs to, which policies those
        /// groups were granted anywhere on the chain, and whether any of those policies carries
        /// the permission.
        /// <para>
        /// <b>An unadministered resource is not a forbidden one - to use.</b> When no grant exists
        /// anywhere on the chain, the answer is <see langword="true"/>: the installation has never
        /// expressed a restriction, and reading "nobody said yes" as "everybody is refused" would
        /// make every record unreachable the moment a guard is added to it. As soon as a single
        /// grant exists on the chain, the chain is administered and the permission is enforced.
        /// An <em>administrative</em> permission is the exception
        /// (<see cref="PermissionImplication.IsAdministrative"/>): on an unadministered chain it
        /// is granted to the members of <c>Group.AdministratorsId</c> alone.
        /// </para>
        /// <para>
        /// Every caller is a member of the built-in <c>Group.AnonymousId</c>, every caller that
        /// resolves to an account also of <c>Group.AuthenticatedId</c>; a grant to either reaches
        /// its members without any stored membership. A workspace grant reaches beneath the
        /// workspace as <see cref="PermissionImplication"/> describes.
        /// </para>
        /// </remarks>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required, an <c>IIdentityPermission</c>.</param>
        /// <param name="resources">The resource and the resources that contain it, most specific first.</param>
        /// <returns><see langword="true"/> when the action may proceed.</returns>
        bool IsGranted(Guid identityId, Type permission, params PermissionResource[] resources);

        /// <summary>
        /// Returns the classes on whose chain (class, workspace) an identity does <em>not</em>
        /// hold a permission - <see cref="IsGranted"/> answered for every class at once, for a
        /// caller that narrows a query rather than judging one record.
        /// </summary>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused classes.</returns>
        IReadOnlySet<Guid> GetRefusedClassIds(Guid identityId, Type permission);

        /// <summary>
        /// Returns the workspaces on whose chain an identity does <em>not</em> hold a permission,
        /// for a caller that narrows a list of workspaces.
        /// </summary>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused workspaces.</returns>
        IReadOnlySet<Guid> GetRefusedWorkspaceIds(Guid identityId, Type permission);

        /// <summary>
        /// Returns the resources of one scope - each its own whole chain, such as a dashboard -
        /// on which an identity does <em>not</em> hold a non-administrative permission.
        /// </summary>
        /// <param name="scope">The kind of resource, as named in <see cref="PermissionScope"/>.</param>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the refused resources.</returns>
        IReadOnlySet<Guid> GetRefusedIds(string scope, Guid identityId, Type permission);

        /// <summary>
        /// Returns the resources of one scope on which an identity holds a permission
        /// <em>through a grant</em> - the reading for a resource that is private until it is
        /// shared, such as a saved search.
        /// </summary>
        /// <remarks>
        /// The counterpart of <see cref="GetRefusedIds"/> with the opposite reading of silence:
        /// a resource nobody granted anything on is not in the answer, because nothing was
        /// shared. The implicit groups count as usual, so a grant to <c>Group.AuthenticatedId</c>
        /// shares with every signed-in caller.
        /// </remarks>
        /// <param name="scope">The kind of resource, as named in <see cref="PermissionScope"/>.</param>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="permission">The permission type required.</param>
        /// <returns>The ids of the resources a grant opens to the identity.</returns>
        IReadOnlySet<Guid> GetGrantedIds(string scope, Guid identityId, Type permission);

        /// <summary>
        /// Grants a group a policy on a resource.
        /// </summary>
        /// <remarks>
        /// Granting what is already granted changes nothing and is reported as the existing grant,
        /// so a repeated click cannot produce a duplicate row.
        /// </remarks>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <param name="groupId">The group the policy is granted to.</param>
        /// <param name="policy">The registered name of the policy.</param>
        /// <returns>The grant, or null when the group or the policy is not known.</returns>
        PermissionAssignment Assign(string scope, string scopeId, Guid groupId, string policy);

        /// <summary>
        /// Withdraws a policy from a group on a resource.
        /// </summary>
        /// <param name="scope">The kind of resource.</param>
        /// <param name="scopeId">The identifier of the resource within its scope.</param>
        /// <param name="groupId">The group the policy is withdrawn from.</param>
        /// <param name="policy">The registered name of the policy.</param>
        /// <returns>True when a grant was withdrawn; false when there was none.</returns>
        bool Revoke(string scope, string scopeId, Guid groupId, string policy);
    }
}
