using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebIdentity;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Holds when the caller holds a policy on the resource the route names - the fragment
    /// counterpart of <see cref="PageAuthorization"/>.
    /// </summary>
    /// <remarks>
    /// <b>Do not put <c>[Policy&lt;…&gt;]</c> on a fragment; put this condition there.</b> WebExpress
    /// evaluates a fragment's policies against the framework identity's <em>global</em> group
    /// policies (<c>IdentityManager.CheckAccess</c>), which KleeneStar neither seeds nor
    /// administers - so a fragment carrying one is hidden from everybody, the administrator
    /// included; the workspace sidebar went blank that way. KleeneStar grants policies on a
    /// <em>resource</em> (a workspace, a class) through <see cref="WebManager.IPermissionManager"/>,
    /// and this condition asks that question: the permissions the policy declares
    /// (<c>[Permission&lt;…&gt;]</c>), on the chain of the object the route names, else of its
    /// workspace. A chain nobody administered is open, one grant on it makes it enforced - the
    /// semantics of the permission manager, the same as the pages'. A route that names no
    /// resource is on no chain and is not refused.
    /// </remarks>
    /// <typeparam name="TPolicy">The policy the caller must hold.</typeparam>
    public sealed class PolicyCondition<TPolicy> : ICondition
        where TPolicy : IIdentityPolicy
    {
        /// <summary>
        /// The permissions each policy declares, read once per policy type.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<Type>> _permissions = new();

        /// <summary>
        /// Determines whether the caller holds the policy on the route's resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when every permission of the policy is granted.</returns>
        public bool Fulfillment(IRequest request)
        {
            var chain = Chain(request);

            if (chain.Length == 0)
            {
                return true;
            }

            return PermissionsOf(typeof(TPolicy))
                .All(permission => PageAuthorization.IsGranted(request, permission, chain));
        }

        /// <summary>
        /// Returns the resource chain the route names: the object's (class, workspace), else the
        /// workspace's, else the class's (class, workspace) when the route names a class or a
        /// record of its structure.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The chain; empty when the route names no resource.</returns>
        private static PermissionResource[] Chain(IRequest request)
        {
            var objectKey = request?.GetParameter<ObjectKeyParameter>();

            if (!string.IsNullOrEmpty(objectKey?.Value))
            {
                // the object is looked up without the caller's clearance: whether the caller may
                // see the record is the security level's question, asked where it is shown;
                // this one is about the permissions on its class and workspace
                using (CoreHub.SecurityLevelManager.BeginUnrestricted())
                {
                    var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);

                    if (@object is not null)
                    {
                        return PageAuthorization.ChainOf(@object);
                    }
                }
            }

            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>();

            if (!string.IsNullOrEmpty(workspaceKey?.Value))
            {
                return PageAuthorization.ChainOf(CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey.Value));
            }

            // a class administration page names a class, or a record of its structure
            var @class = request is null ? null : RouteAuthorization.ResolveClass(request);

            return PageAuthorization.ChainOf(@class);
        }

        /// <summary>
        /// Reads the permissions a policy declares.
        /// </summary>
        /// <param name="policy">The policy type.</param>
        /// <returns>The permission types.</returns>
        private static IReadOnlyList<Type> PermissionsOf(Type policy)
        {
            return _permissions.GetOrAdd(policy, type => [.. type.CustomAttributes
                .Where(x => x.AttributeType.IsGenericType
                    && x.AttributeType.GetGenericTypeDefinition() == typeof(PermissionAttribute<>))
                .Select(x => x.AttributeType.GenericTypeArguments[0])]);
        }
    }
}
