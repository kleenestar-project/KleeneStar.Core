using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_
{
    /// <summary>
    /// Serves the permission dialog of a saved search: which group holds which policy on it.
    /// </summary>
    /// <remarks>
    /// Only the owner and a group granted <c>savedsearch_admin_policy</c> may read or change
    /// the grants - reading them already says with whom the search is shared.
    /// </remarks>
    [IncludeSubPaths]
    [Cache]
    public sealed class Permission : RestApiPermissionScoped
    {
        /// <summary>
        /// Gets the kind of resource this endpoint administers.
        /// </summary>
        protected override string Scope => PermissionScope.SavedSearch;

        /// <summary>
        /// Determines whether the caller may decide who else may use the saved search.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the request may proceed.</returns>
        protected override bool Authorized(IRequest request)
        {
            var savedSearch = CoreHub.SavedSearchManager.GetSavedSearch(request?.GetParameter<SavedSearchIdParameter>());

            return SavedSearchAuthorization.MayAdminister(savedSearch, request);
        }

        /// <summary>
        /// Returns the saved search the request addresses.
        /// </summary>
        /// <param name="request">The request whose route names the saved search.</param>
        /// <returns>The saved-search id, or null when the route addresses none.</returns>
        protected override string ResolveScopeId(IRequest request)
        {
            return CoreHub.SavedSearchManager.GetSavedSearch(request?.GetParameter<SavedSearchIdParameter>())?.Id.ToString();
        }
    }
}
