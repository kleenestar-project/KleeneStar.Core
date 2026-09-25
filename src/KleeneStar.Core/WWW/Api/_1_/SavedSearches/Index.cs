using KleeneStar.Core.WebFragment.Search;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearches
{
    // The entity type SavedSearch collides with the sibling WWW.SavedSearch namespace;
    // alias it (inside the namespace block) so the bare name binds to the entity.
    using SavedSearch = KleeneStar.Model.Entities.SavedSearch;

    /// <summary>
    /// Provides CRUD operations for saved-search items via a REST API. Backs the add, edit,
    /// and delete modal forms reached from the search page.
    /// </summary>
    /// <remarks>
    /// A caller sees their own saved searches and those shared with them
    /// (<see cref="SavedSearchAuthorization"/>); changing and deleting one needs the
    /// permission its grants carry, and every refusal is a <c>403</c>. The owner stays the
    /// identity that created the saved search - a payload cannot hand it to somebody else, and
    /// only the owner's star is stored on the row, so another editor cannot move it.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<SavedSearch>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Retrieves the response for the specified request, once the caller may do what the
        /// requested mode prepares.
        /// </summary>
        /// <param name="request">The request object. Must not be null.</param>
        /// <returns>The retrieval response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var savedSearch = SavedSearchAuthorization.ResolveById(request);

            var authorized = request?.GetParameter("mode")?.Value switch
            {
                "edit" => SavedSearchAuthorization.MayUpdate(savedSearch, request),
                "delete" => SavedSearchAuthorization.MayDelete(savedSearch, request),
                "clone" => SavedSearchAuthorization.MayRead(savedSearch, request),
                "new" => SavedSearchAuthorization.MayCreate(request),
                _ => true
            };

            return authorized ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Creates a saved search owned by the caller, or a copy of one they may see.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            var original = SavedSearchAuthorization.ResolveById(request);

            if (!SavedSearchAuthorization.MayCreate(request)
                || (original is not null && !SavedSearchAuthorization.MayRead(original, request)))
            {
                return new ResponseForbidden();
            }

            return base.Create(request);
        }

        /// <summary>
        /// Changes a saved search, once the caller may.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return SavedSearchAuthorization.MayUpdate(SavedSearchAuthorization.ResolveById(request), request)
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Deletes a saved search, once the caller may.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return SavedSearchAuthorization.MayDelete(SavedSearchAuthorization.ResolveById(request), request)
                ? base.Delete(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Creates a new query context backed by the application database.
        /// </summary>
        /// <returns>An <see cref="IQueryContext"/> instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the saved searches the caller may see matching the specified query: their
        /// own and those shared with them. The update and delete item lookups of the base class
        /// funnel through this method as well, so a saved search the caller may not see yields
        /// nothing (404) rather than a record.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The matching saved searches the caller may see.</returns>
        protected override IEnumerable<SavedSearch> Retrieve(IQuery<SavedSearch> query, IQueryContext context, IRequest request)
        {
            return CoreHub.SavedSearchManager.GetSavedSearches(Visible(query, request), context);
        }

        /// <summary>
        /// Retrieves the data a new saved search starts with: the expression the search page had
        /// on screen when the dialog was opened from there, carried in the <c>wql</c> parameter.
        /// </summary>
        /// <param name="request">The request context.</param>
        /// <returns>The data required to initialize a new saved search for creation.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            var wql = request?.GetParameter(SavedSearchRun.WqlParameter)?.Value;

            return string.IsNullOrWhiteSpace(wql)
                ? base.RetrieveForCreate(request)
                : new RestApiCrudResultRetrieve()
                {
                    Data = new Dictionary<string, object> { [nameof(SavedSearch.Query)] = wql }
                };
        }

        /// <summary>
        /// Retrieves a saved search the caller may change for the edit dialog. When the dialog
        /// was opened from the search page running it, the expression on screen replaces the
        /// stored query, so saving stores what the user looks at.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request context.</param>
        /// <returns>The saved search for update, or none when the caller may not see it.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<SavedSearch> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SavedSearchManager.GetSavedSearches(Visible(query, request), context)
                .FirstOrDefault();
            var wql = request?.GetParameter(SavedSearchRun.WqlParameter)?.Value;

            // the record is read in a context that is disposed unsaved, so this changes the
            // answer and nothing stored
            if (data is not null && !string.IsNullOrWhiteSpace(wql))
            {
                data.Query = wql;
            }

            // the prose editor refuses a null value by throwing, and the form fills its fields
            // in the order of the answer - so an unwritten description stopped every field after
            // it, the query among them, from being filled at all
            if (data is not null)
            {
                data.Description ??= string.Empty;
            }

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the saved search identified by the query in preparation for deletion.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request context.</param>
        /// <returns>The saved search and metadata for deletion, or none when the caller may not see it.</returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<SavedSearch> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SavedSearchManager.GetSavedSearches(Visible(query, request), context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Name);
        }

        /// <summary>
        /// Validates the data for create or update operations: a name and a query are
        /// required, and the query has to be an expression the search page can run.
        /// </summary>
        /// <param name="existingItem">The currently persisted item (null for create).</param>
        /// <param name="payload">The dynamic payload containing the fields.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult Validate(SavedSearch existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var creating = existingItem is null;

            var (name, nameSent) = ReadField(payload, nameof(SavedSearch.Name));
            var (query, querySent) = ReadField(payload, nameof(SavedSearch.Query));

            if ((creating || nameSent) && string.IsNullOrWhiteSpace(name))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:search.saved.name.validation.required"), nameof(SavedSearch.Name));
            }

            if ((creating || querySent) && string.IsNullOrWhiteSpace(query))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:search.saved.query.validation.required"), nameof(SavedSearch.Query));
            }
            else if (querySent && !WqlFilter.TryValidate<Model.Entities.Object>(query, out var error))
            {
                // the parser reports its reason as an i18n key of the index component
                result.Add($"{I18N.Translate(request, "kleenestar.core:search.saved.query.validation.invalid")} {I18N.Translate(request, error)}", nameof(SavedSearch.Query));
            }

            return result;
        }

        /// <summary>
        /// Persists a newly created saved search owned by the calling identity and answers the
        /// address that runs it, which <c>objectcreated.js</c> opens.
        /// </summary>
        /// <param name="fieldMap">The form payload (Name, Query, Description, Starred).</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="newItem">The created saved search.</param>
        /// <returns>The create result.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out SavedSearch newItem)
        {
            newItem = NewOwned(fieldMap, request);

            // the results were on screen in a layout when the search was saved - the running
            // saved search's, or the caller's own - and the new saved search opens in it
            newItem.Columns ??= global::KleeneStar.Core.WWW.Api._1_.Objects.Table.CurrentLayout(request);

            CoreHub.SavedSearchManager.Add(newItem);

            return Created(newItem);
        }

        /// <summary>
        /// Clones an existing saved search into a new one owned by the calling identity. The
        /// grants of the original are not copied: the copy is private until its owner shares it.
        /// </summary>
        /// <param name="existingItem">The source saved search.</param>
        /// <param name="fieldMap">The form payload.</param>
        /// <param name="request">The HTTP request.</param>
        /// <param name="newItem">The created saved search.</param>
        /// <returns>The create result.</returns>
        protected override IRestApiCrudResultCreate Clone(SavedSearch existingItem, RestApiCrudFormData fieldMap, IRequest request, out SavedSearch newItem)
        {
            newItem = NewOwned(fieldMap, request);
            newItem.Query ??= existingItem?.Query;
            newItem.Description ??= existingItem?.Description;
            newItem.Columns ??= existingItem?.Columns;

            CoreHub.SavedSearchManager.Add(newItem);

            return Created(newItem);
        }

        /// <summary>
        /// Updates an existing saved search. The owner, the state and the owner's star are not
        /// the payload's to change - the star only when the owner is the one saving.
        /// </summary>
        /// <param name="existingItem">The currently persisted item.</param>
        /// <param name="payload">The dynamic payload containing updated fields.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate Update(SavedSearch existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var ownerId = existingItem.OwnerId;
            var state = existingItem.State;
            var starred = existingItem.Starred;
            var created = existingItem.Created;
            var lastUsed = existingItem.LastUsed;

            var res = base.Update(existingItem, payload, request);

            existingItem.OwnerId = ownerId;
            existingItem.State = state;
            existingItem.Created = created;
            existingItem.LastUsed = lastUsed;

            if (!SavedSearchAuthorization.IsOwner(existingItem, request))
            {
                existingItem.Starred = starred;
            }

            CoreHub.SavedSearchManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the specified saved search.
        /// </summary>
        /// <param name="existingItem">The currently persisted item to delete.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The delete result.</returns>
        protected override IRestApiCrudResultDelete Delete(SavedSearch existingItem, IRequest request)
        {
            CoreHub.SavedSearchManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// Narrows a query to the active saved searches the caller may see: their own and those
        /// shared with them.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request naming the caller.</param>
        /// <returns>The narrowed query.</returns>
        private static IQuery<SavedSearch> Visible(IQuery<SavedSearch> query, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return query.Where(x => false);
            }

            var shared = CoreHub.PermissionManager
                .GetGrantedIds(PermissionScope.SavedSearch, identityId, typeof(SavedSearchReadPermission))
                .ToList();

            return query.Where(x => x.State == SavedSearchState.Active && (x.OwnerId == identityId || shared.Contains(x.Id)));
        }

        /// <summary>
        /// Builds a new saved search owned by the caller from a payload, keeping the fields that
        /// are not the payload's to set.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="request">The request naming the caller.</param>
        /// <returns>The new saved search, not yet stored.</returns>
        private static SavedSearch NewOwned(RestApiCrudFormData fieldMap, IRequest request)
        {
            var now = DateTime.UtcNow;
            var item = new SavedSearch(Guid.NewGuid());

            fieldMap.BindTo(item);

            item.OwnerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            item.State = SavedSearchState.Active;
            item.LastUsed = now;
            item.Created = now;
            item.Updated = now;

            return item;
        }

        /// <summary>
        /// Answers a create with the address that runs the new saved search. The search page
        /// around the dialog - its sidebar, its headline - is rendered once, and running the new
        /// search renders it anew with the search in place. <c>created</c> marks the answer for
        /// <c>objectcreated.js</c>; there is no <c>message</c>, which would keep the dialog open.
        /// </summary>
        /// <param name="savedSearch">The saved search that was created.</param>
        /// <returns>The create result.</returns>
        private static RestApiCrudResultCreate Created(SavedSearch savedSearch)
        {
            return new RestApiCrudResultCreate
            {
                Data = new
                {
                    created = true,
                    id = savedSearch.Id,
                    uri = SavedSearchRun.RunUri(savedSearch)?.ToString()
                }
            };
        }

        /// <summary>
        /// Reads a field of the payload, whichever case the client wrote its key in.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="field">The field name.</param>
        /// <returns>The value, and whether the payload carried the field at all.</returns>
        private static (string Value, bool Sent) ReadField(RestApiCrudFormData payload, string field)
        {
            if (payload is null)
            {
                return (null, false);
            }

            if (payload.TryGetValue(field.ToLowerInvariant(), out var lower))
            {
                return (lower?.ToString(), true);
            }

            return payload.TryGetValue(field, out var exact) ? (exact?.ToString(), true) : (null, false);
        }
    }
}
