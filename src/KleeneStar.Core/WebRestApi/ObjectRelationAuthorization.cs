using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using System;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers whether a request may read or change the relations it addresses.
    /// </summary>
    /// <remarks>
    /// Relations are governed by the object permission model rather than by one of their own: a
    /// relation is a statement <i>about</i> objects, so the right to make it follows the right to
    /// edit them. Administering the relation <i>catalog</i> is a class-level concern instead,
    /// because a definition outlives every object that uses it.
    /// <para>
    /// The chain runs from the class of the object to its workspace, and <b>not</b> through the
    /// object itself: an individual record carries no grants. Who may see a record is decided by
    /// its security level; what may be done with the records of a data structure is granted on
    /// the class. A model in which every object had to be granted individually would be
    /// unusable, and the class and the workspace are the units an installation administers.
    /// </para>
    /// </remarks>
    internal static class ObjectRelationAuthorization
    {
        /// <summary>
        /// Determines whether the caller may read the relations of an object.
        /// </summary>
        /// <param name="object">The object whose relations are addressed.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the relations may be answered.</returns>
        public static bool MayRead(ObjectEntity @object, IRequest request)
        {
            return Check(@object, request, typeof(ObjectReadPermission));
        }

        /// <summary>
        /// Determines whether the caller may establish, change or remove a relation of an object.
        /// </summary>
        /// <param name="object">The object whose relations are addressed.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayWrite(ObjectEntity @object, IRequest request)
        {
            return Check(@object, request, typeof(ObjectRelationPermission));
        }

        /// <summary>
        /// Determines whether the caller may read the relation catalog administered from a class.
        /// </summary>
        /// <param name="classId">The class the surface is administered from.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the catalog may be answered.</returns>
        public static bool MayReadCatalog(Guid classId, IRequest request)
        {
            return CheckClass(classId, request, typeof(ClassReadPermission));
        }

        /// <summary>
        /// Determines whether the caller may define, change, reorder or drop a relation.
        /// </summary>
        /// <param name="classId">The class the surface is administered from.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayWriteCatalog(Guid classId, IRequest request)
        {
            return CheckClass(classId, request, typeof(ClassUpdatePermission));
        }

        /// <summary>
        /// Evaluates a permission against the class of an object and the workspace it is filed
        /// in. The object itself is not a link in the chain - see the remarks on the type.
        /// </summary>
        /// <param name="object">The object, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <param name="permission">The permission required.</param>
        /// <returns><see langword="true"/> when the caller holds the permission.</returns>
        private static bool Check(ObjectEntity @object, IRequest request, Type permission)
        {
            // an unresolvable object is not an authorization question - the endpoint answers it
            // as not found, and refusing here would turn a wrong key into a permission error.
            // An object the caller is not cleared for never reaches here either: the object
            // manager did not answer it, so the endpoint saw no object at all
            if (@object is null)
            {
                return true;
            }

            return CoreHub.PermissionManager.IsGranted
            (
                CoreHub.SessionManager.GetCurrentIdentityId(request),
                permission,
                new PermissionResource(PermissionScope.Class, @object.ClassId.ToString()),
                new PermissionResource(PermissionScope.Workspace, @object.WorkspaceId.ToString())
            );
        }

        /// <summary>
        /// Evaluates a permission against a class and the workspace it belongs to.
        /// </summary>
        /// <param name="classId">The class, may be unknown.</param>
        /// <param name="request">The incoming request.</param>
        /// <param name="permission">The permission required.</param>
        /// <returns><see langword="true"/> when the caller holds the permission.</returns>
        private static bool CheckClass(Guid classId, IRequest request, Type permission)
        {
            var @class = classId == Guid.Empty ? null : CoreHub.ClassManager.GetClass(classId);

            if (@class is null)
            {
                return true;
            }

            return CoreHub.PermissionManager.IsGranted
            (
                CoreHub.SessionManager.GetCurrentIdentityId(request),
                permission,
                new PermissionResource(PermissionScope.Class, @class.Id.ToString()),
                new PermissionResource(PermissionScope.Workspace, @class.WorkspaceId.ToString())
            );
        }

        /// <summary>
        /// Reads the class the route of the catalog endpoint addresses.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The class id, or <see cref="Guid.Empty"/> when the route names none.</returns>
        public static Guid ResolveClassId(IRequest request)
        {
            return Guid.TryParse(request?.GetParameter<ClassIdParameter>()?.Value, out var id)
                ? id
                : Guid.Empty;
        }
    }
}
