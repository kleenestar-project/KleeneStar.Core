using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using System;
using WebExpress.WebCore.WebMessage;
using SavedSearchEntity = KleeneStar.Model.Entities.SavedSearch;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers whether a request may run, change, delete or share a saved search.
    /// </summary>
    /// <remarks>
    /// A saved search belongs to its owner, who may do everything with it. Its permission
    /// dialog is how it is shared: a group granted a <c>savedsearch_…</c> policy on it sees it
    /// and may do what the policy carries. Nobody else sees it - a saved search nobody shared
    /// is <b>private</b>, the opposite reading of every other resource, where silence means open
    /// (<see cref="WebManager.ISavedSearchManager.IsGranted"/>).
    /// <para>
    /// The pages (<c>WWW/SavedSearch/{id}/Edit</c>, <c>Delete</c>, <c>Permission</c>) and the
    /// endpoints ask the same question, so a dialog is never offered for an act the endpoint
    /// refuses. A saved search that does not resolve is not a permission question - an
    /// endpoint answers it as not found. Creating one needs a signed-in caller, who becomes its
    /// owner.
    /// </para>
    /// </remarks>
    internal static class SavedSearchAuthorization
    {
        /// <summary>
        /// Determines whether the caller may create a saved search.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> for a signed-in caller.</returns>
        public static bool MayCreate(IRequest request)
        {
            return RouteAuthorization.IsSignedIn(request);
        }

        /// <summary>
        /// Determines whether the caller may see and run a saved search.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the saved search may be shown.</returns>
        public static bool MayRead(SavedSearchEntity savedSearch, IRequest request)
        {
            return Check(savedSearch, request, typeof(SavedSearchReadPermission));
        }

        /// <summary>
        /// Determines whether the caller may change a saved search.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayUpdate(SavedSearchEntity savedSearch, IRequest request)
        {
            return Check(savedSearch, request, typeof(SavedSearchUpdatePermission));
        }

        /// <summary>
        /// Determines whether the caller may delete a saved search.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the deletion may proceed.</returns>
        public static bool MayDelete(SavedSearchEntity savedSearch, IRequest request)
        {
            return Check(savedSearch, request, typeof(SavedSearchDeletePermission));
        }

        /// <summary>
        /// Determines whether the caller may decide who else may use a saved search.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the grants may be read and changed.</returns>
        public static bool MayAdminister(SavedSearchEntity savedSearch, IRequest request)
        {
            return Check(savedSearch, request, typeof(SavedSearchManageProfilesPermission));
        }

        /// <summary>
        /// Determines whether the caller owns a saved search - the one who may star it and whose
        /// recently-used list running it updates.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> for the owner.</returns>
        public static bool IsOwner(SavedSearchEntity savedSearch, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return savedSearch is not null && identityId != Guid.Empty && savedSearch.OwnerId == identityId;
        }

        /// <summary>
        /// Reads the saved search the <c>id</c> query parameter of a CRUD request addresses.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The saved search, or <see langword="null"/> when the id resolves to none.</returns>
        public static SavedSearchEntity ResolveById(IRequest request)
        {
            return ContentAuthorization.ReadId(request) is { } id
                ? CoreHub.SavedSearchManager.GetSavedSearch(id)
                : null;
        }

        /// <summary>
        /// Asks the saved-search manager for a permission of the caller.
        /// </summary>
        /// <param name="savedSearch">The saved search, may be absent.</param>
        /// <param name="request">The incoming request.</param>
        /// <param name="permission">The permission required.</param>
        /// <returns><see langword="true"/> when the caller holds it.</returns>
        private static bool Check(SavedSearchEntity savedSearch, IRequest request, Type permission)
        {
            return CoreHub.SavedSearchManager.IsGranted(
                savedSearch,
                CoreHub.SessionManager.GetCurrentIdentityId(request),
                permission);
        }
    }
}
