using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using SavedSearchEntity = KleeneStar.Model.Entities.SavedSearch;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_
{
    /// <summary>
    /// Serves the quickfilter bar of a saved search: the filters defined for it, as chips over its
    /// results.
    /// </summary>
    /// <remarks>
    /// The filters are the users' own (<see cref="CustomQuickfilter"/>), bound to the view
    /// <see cref="ViewKey"/> and to the saved search as their context - so each saved search has
    /// its own bar. A filter is its author's; switched to shared it is offered to everybody who
    /// may see the saved search, which is the only audience the bar has: a caller who may not
    /// read the saved search gets no chips and may define none. A filter is changed or removed
    /// by its author, or by whoever may change the saved search it belongs to.
    /// </remarks>
    [Cache]
    public sealed class Quickfilter : RestApiQuickfilter<Model.Entities.Object>
    {
        /// <summary>
        /// The key under which the quickfilters of saved searches are stored. The bar, the filter
        /// dialogs and the results table have to agree on it.
        /// </summary>
        public const string ViewKey = "savedsearch";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves the chips of the bar: the filters of the saved search the caller may see.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request naming the saved search.</param>
        /// <returns>The chips; none for a saved search the caller may not see.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            var savedSearch = Resolve(request);

            return SavedSearchAuthorization.MayRead(savedSearch, request)
                ? CustomQuickfilterSupport.Items(ViewKey, savedSearch.Id.ToString(), request)
                : [];
        }

        /// <summary>
        /// Returns the record the edit dialog of a filter loads.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request naming the saved search.</param>
        /// <param name="id">The id of the filter.</param>
        /// <returns>The record, or null when the caller may not change the filter.</returns>
        protected override object RetrieveItem(IQueryContext context, IRequest request, string id)
        {
            return MayChange(id, request) ? CustomQuickfilterSupport.Read(id, ViewKey) : null;
        }

        /// <summary>
        /// Stores a filter the caller defined for the saved search.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request naming the saved search.</param>
        /// <param name="payload">The values the client supplied.</param>
        /// <returns>The stored filter, or null when the caller may not see the saved search.</returns>
        protected override RestApiQuickfilterItem CreateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            var savedSearch = Resolve(request);

            return SavedSearchAuthorization.MayRead(savedSearch, request)
                ? CustomQuickfilterSupport.Create(payload, ViewKey, savedSearch.Id.ToString(), request)
                : null;
        }

        /// <summary>
        /// Changes a filter of the saved search.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request naming the saved search.</param>
        /// <param name="payload">The values the client supplied.</param>
        /// <returns>The changed filter, or null when the caller may not change it.</returns>
        protected override RestApiQuickfilterItem UpdateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            return MayChange(payload?.Id, request)
                ? CustomQuickfilterSupport.Update(payload, ViewKey, request)
                : null;
        }

        /// <summary>
        /// Removes a filter of the saved search.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request naming the saved search.</param>
        /// <param name="id">The id of the filter.</param>
        /// <returns>True when the filter was removed.</returns>
        protected override bool DeleteItem(IQueryContext context, IRequest request, string id)
        {
            return MayChange(id, request) && CustomQuickfilterSupport.Delete(id, ViewKey);
        }

        /// <summary>
        /// Returns the saved search the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The saved search, or null.</returns>
        private static SavedSearchEntity Resolve(IRequest request)
        {
            return CoreHub.SavedSearchManager.GetSavedSearch(request?.GetParameter<SavedSearchIdParameter>());
        }

        /// <summary>
        /// Determines whether the caller may change or remove a filter: it has to belong to the
        /// saved search the route names, which the caller may see, and be theirs - or the saved
        /// search has to be one they may change.
        /// </summary>
        /// <param name="filterId">The chip id of the filter.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        private static bool MayChange(string filterId, IRequest request)
        {
            var savedSearch = Resolve(request);
            var id = CustomQuickfilter.ParseFilterId(filterId);
            var filter = id is { } value ? CoreHub.CustomQuickfilterManager.GetCustomQuickfilter(value) : null;

            if (filter is null
                || !SavedSearchAuthorization.MayRead(savedSearch, request)
                || !string.Equals(filter.ViewKey, ViewKey, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(filter.ContextKey, savedSearch.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return (identityId != Guid.Empty && filter.OwnerId == identityId)
                || SavedSearchAuthorization.MayUpdate(savedSearch, request);
        }
    }
}
