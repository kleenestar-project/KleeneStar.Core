using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebNavigator;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.NavigatorLinks
{
    /// <summary>
    /// Provides CRUD operations for navigator link items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<NavigatorLink>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers a read; the reads that prepare a change (the new, edit, clone and delete
        /// dialogs) only for an administrator.
        /// </summary>
        /// <remarks>
        /// The links appear in the app navigator of every user, so a write open to anybody plants a link in front of everybody. The administrators are those of the accounts (<see cref="AccountAuthorization"/>),
        /// the only installation-wide administration there is.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var preparesChange = request?.GetParameter("mode")?.Value is "edit" or "delete" or "clone" or "new";

            return !preparesChange || AccountAuthorization.IsAdministrator(request)
                ? base.Retrieve(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Creates or clones an entry of the navigator links, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Create(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Changes an entry of the navigator links, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Removes an entry of the navigator links, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Delete(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Determines the icon of a navigator link from the favicon of the site it points at.
        /// </summary>
        /// <remarks>
        /// The resolution is bounded and returns nothing rather than throwing when the target is
        /// unreachable, so the supplied fallback keeps the link usable. It is awaited here because
        /// the icon has to be part of the record being written; the resolver caps the wait so a slow
        /// target cannot hold the save open.
        /// </remarks>
        /// <param name="address">The address configured on the link.</param>
        /// <param name="fallback">The icon to keep when no favicon can be determined.</param>
        /// <returns>The resolved icon, or the fallback.</returns>
        private static ImageIcon ResolveIcon(string address, ImageIcon fallback)
        {
            var normalized = NavigatorLinkAddress.Normalize(address);

            // an internal route is served by this application, so its icon is known without asking
            // anyone and no outbound request is made at all
            if (NavigatorLinkAddress.IsInternal(normalized))
            {
                var applicationIcon = CoreHub.ApplicationContext?.Icon?.ToUri()?.ToString();

                return string.IsNullOrEmpty(applicationIcon)
                    ? fallback
                    : ImageIcon.FromString(applicationIcon);
            }

            var favicon = FaviconResolver.ResolveAsync(address)
                .GetAwaiter()
                .GetResult();

            return string.IsNullOrEmpty(favicon)
                ? fallback
                : ImageIcon.FromString(favicon);
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>
        /// An IQueryContext instance that can be used to execute queries.
        /// </returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// A collection representing the filtered set of index items.
        /// The collection may be empty if no items match the query.
        /// </returns>
        protected override IEnumerable<NavigatorLink> Retrieve(IQuery<NavigatorLink> query, IQueryContext context, IRequest request)
        {
            return CoreHub.NavigatorLinkManager.GetNavigatorLinks(query, context);
        }

        /// <summary>
        /// Retrieves a result object containing default values and metadata for
        /// cloning a item.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">The request.</param>
        /// <returns>
        /// A result instance representing the data and metadata required
        /// to initialize a new item for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<NavigatorLink> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.NavigatorLinkManager.GetNavigatorLinks(query, context)
                .FirstOrDefault();

            var newItem = new NavigatorLink()
            {
                Name = data?.Name + " (Copy)",
                Description = data?.Description,
                Uri = data?.Uri,
                Ordinal = data?.Ordinal ?? 0,
                Icon = data?.Icon,
                State = NavigatorLinkState.Active
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a navigator link identified by the specified id for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the navigator link associated with the specified id.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<NavigatorLink> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.NavigatorLinkManager.GetNavigatorLinks(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the navigator link identified by the specified id in preparation for deletion.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for
        /// the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the navigator link and related information required
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<NavigatorLink> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.NavigatorLinkManager.GetNavigatorLinks(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Id.ToString());
        }

        /// <summary>
        /// Persists the newly created resource.
        /// </summary>
        /// <param name="fieldMap">
        /// The dynamic payload containing the fields required to create the resource.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context for the creation process.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly created index item,
        /// or the default value if creation was not successful.
        /// </param>
        /// <returns>
        /// A result object containing information about the create operation,
        /// including the created resource.
        /// </returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out NavigatorLink newItem)
        {
            var id = Guid.NewGuid();
            newItem = new NavigatorLink(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = NavigatorLinkState.Active
            };

            fieldMap.BindTo(newItem);

            newItem.Icon = ResolveIcon(newItem.Uri, newItem.Icon);

            CoreHub.NavigatorLinkManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new instance by cloning data from the specified form data and
        /// adds it to the navigator link manager.
        /// </summary>
        /// <param name="existingItem">
        /// The existing item to use as a reference for the clone operation. This parameter
        /// is not modified.
        /// </param>
        /// <param name="fieldMap">
        /// The form data containing field values to bind to the new instance. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current request context for the operation.
        /// </param>
        /// <param name="newItem">
        /// When this method returns, contains the newly created instance populated
        /// with the provided form data.
        /// </param>
        /// <returns>
        /// A result object indicating the outcome of the create operation.
        /// </returns>
        protected override IRestApiCrudResultCreate Clone(NavigatorLink existingItem, RestApiCrudFormData fieldMap, IRequest request, out NavigatorLink newItem)
        {
            var id = Guid.NewGuid();
            newItem = new NavigatorLink(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = NavigatorLinkState.Active
            };

            fieldMap.BindTo(newItem);

            newItem.Icon = ResolveIcon(newItem.Uri, newItem.Icon);

            CoreHub.NavigatorLinkManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the data record.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item.
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing the updated navigator link.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        protected override IRestApiCrudResultUpdate Update(NavigatorLink existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var previousAddress = existingItem?.Uri;
            var previousIcon = existingItem?.Icon;

            var res = base.Update(existingItem, payload, request);

            if (!string.Equals(previousAddress, existingItem.Uri, StringComparison.Ordinal))
            {
                existingItem.Icon = ResolveIcon(existingItem.Uri, previousIcon);
            }

            CoreHub.NavigatorLinkManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the specified resource.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item that is to be deleted.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context for the delete operation.
        /// </param>
        /// <returns>
        /// A result object containing information about the delete operation.
        /// </returns>
        protected override IRestApiCrudResultDelete Delete(NavigatorLink existingItem, IRequest request)
        {
            CoreHub.NavigatorLinkManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
