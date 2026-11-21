using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// The guard a page puts in front of what it does or shows.
    /// </summary>
    /// <remarks>
    /// The framework's own <c>[Policy&lt;T&gt;]</c> on a page asks a different question than
    /// this application administers: it checks the signed-in identity's groups for a policy
    /// <em>anywhere</em>, refuses an anonymous caller outright and knows nothing of the workspace
    /// or class the route addresses - so on an installation that has granted nothing yet it
    /// would refuse everyone, and on one that has it would grant a workspace's administrator
    /// every workspace. This is why every page-level policy attribute in <c>WWW/</c> is
    /// inactive. The rule the application does administer is
    /// <see cref="WebManager.IPermissionManager.IsGranted"/>: a chain from the addressed resource
    /// outwards, unrestricted while nothing on it was ever granted, enforced once something was.
    /// A page asks that rule here, with the chain its route names, before it acts.
    /// <para>
    /// A page cannot answer a status of its own - its result is what its visual tree renders,
    /// and the fragments scoped to it render regardless of what its <c>Process</c> did - so a
    /// refusal is WebExpress's <see cref="ForbiddenException"/>: it ends the page, nothing of it
    /// is sent, and the server answers <em>at the address that was asked for</em> the way it
    /// answers a refused page policy - the forbidden page for a signed-in caller, the sign-in
    /// prompt for one who is not. (It used to redirect to <see cref="WWW.Forbidden.Index"/>,
    /// which lost the address.) An action page gets what it needs (the restore that must not
    /// happen does not) and so does a dialog page (the form that must not be offered is not).
    /// </para>
    /// </remarks>
    public static class PageAuthorization
    {
        /// <summary>
        /// Determines whether the caller of a request holds a permission on a resource chain.
        /// </summary>
        /// <param name="request">The request being answered; <see langword="null"/> asks for the
        /// ambient request, or the identity a <c>BeginIdentity</c> scope stands in with.</param>
        /// <param name="permission">The permission type required.</param>
        /// <param name="resources">The resource and the resources that contain it, most specific first.</param>
        /// <returns><see langword="true"/> when the caller may proceed.</returns>
        public static bool IsGranted(IRequest request, Type permission, params PermissionResource[] resources)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return CoreHub.PermissionManager.IsGranted(identityId, permission, resources);
        }

        /// <summary>
        /// Refuses the request unless its caller holds a permission on a resource chain.
        /// </summary>
        /// <param name="request">The request being answered.</param>
        /// <param name="permission">The permission type required.</param>
        /// <param name="resources">The resource and the resources that contain it, most specific first.</param>
        /// <exception cref="ForbiddenException">The caller is refused; the exception ends the
        /// page's processing and is answered in place.</exception>
        public static void Demand(IRequest request, Type permission, params PermissionResource[] resources)
        {
            if (!IsGranted(request, permission, resources))
            {
                throw Refuse();
            }
        }

        /// <summary>
        /// Builds the refusal of a page, answered in place at the refused address.
        /// </summary>
        /// <returns>The exception to throw.</returns>
        public static ForbiddenException Refuse()
        {
            return new ForbiddenException("The caller holds no permission on the resource the route names.");
        }

        /// <summary>
        /// The chain a workspace is administered on.
        /// </summary>
        /// <param name="workspace">The workspace, may be absent.</param>
        /// <returns>The chain; empty - and therefore unrestricted - when there is no workspace.</returns>
        public static PermissionResource[] ChainOf(Workspace workspace)
        {
            return workspace is null
                ? []
                : [new PermissionResource(PermissionScope.Workspace, workspace.Id.ToString())];
        }

        /// <summary>
        /// The chain a class is administered on: the class, then its workspace.
        /// </summary>
        /// <param name="class">The class, may be absent.</param>
        /// <returns>The chain; empty when there is no class.</returns>
        public static PermissionResource[] ChainOf(Class @class)
        {
            return @class is null
                ? []
                :
                [
                    new PermissionResource(PermissionScope.Class, @class.Id.ToString()),
                    new PermissionResource(PermissionScope.Workspace, @class.WorkspaceId.ToString())
                ];
        }

        /// <summary>
        /// The chain an object is administered on: its class, then its workspace. The object
        /// itself is not a link - an individual record carries no grants, its security level
        /// decides who sees it.
        /// </summary>
        /// <param name="object">The object, may be absent.</param>
        /// <returns>The chain; empty - and therefore unrestricted - when there is no object.</returns>
        public static PermissionResource[] ChainOf(ObjectEntity @object)
        {
            return @object is null
                ? []
                :
                [
                    new PermissionResource(PermissionScope.Class, @object.ClassId.ToString()),
                    new PermissionResource(PermissionScope.Workspace, @object.WorkspaceId.ToString())
                ];
        }
    }
}
