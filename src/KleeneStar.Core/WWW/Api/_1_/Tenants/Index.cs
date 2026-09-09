using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Tenants
{
    /// <summary>
    /// Provides CRUD operations for tenant items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Tenant>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
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
        protected override IEnumerable<Model.Entities.Tenant> Retrieve(IQuery<Model.Entities.Tenant> query, IQueryContext context, IRequest request)
        {
            return CoreHub.TenantManager.GetTenants(query, context);
        }

        /// <summary>
        /// Retrieves the data required to create a new workspace entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new workspace for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            return base.RetrieveForCreate(request);
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
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Tenant> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TenantManager.GetTenants(query, context)
                .FirstOrDefault();

            var newItem = new Model.Entities.Tenant()
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                State = TenantState.Active
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a workspace identified by the specified key for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the workspace associated with the specified key.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Tenant> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TenantManager.GetTenants(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the workspace entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the workspace entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Tenant> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TenantManager.GetTenants(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Id.ToString());
        }

        /// <summary>
        /// Validate the data for create or update operations. When creating, existingItem will 
        /// be null and proposedItem contains the values to create. When updating, existingItem 
        /// is the currently persisted entity and proposedItem contains the incoming values to 
        /// validate.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item (null for create).
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing updated tenants.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        /// <returns>
        /// An IRestApiValidationResult indicating validation success or errors.
        /// </returns>
        protected override IRestApiValidationResult Validate(Model.Entities.Tenant existingItem, RestApiCrudFormData payload, IRequest request)
        {
            return base.Validate(existingItem, payload, request);
        }

        /// <summary>
        /// Persists the newly created resource.
        /// Override this method in derived classes to implement the actual
        /// persistence logic and return a result describing the creation.
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
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Tenant newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Tenant(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = TenantState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.TenantManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new instance by cloning data from the specified form tenants and 
        /// adds it to the class manager.
        /// </summary>
        /// <param name="existingItem">
        /// The existing item to use as a reference for the clone operation. This parameter 
        /// is not modified.
        /// </param>
        /// <param name="fieldMap">
        /// The form data containing field values to bind to the new instance. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current request context for the operation. Provides additional information or 
        /// services required during cloning.
        /// </param>
        /// <param name="newItem">
        /// When this method returns, contains the newly created instance populated 
        /// with the provided form data.
        /// </param>
        /// <returns>
        /// A result object indicating the outcome of the create operation.
        /// </returns>
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Tenant existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Tenant newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Tenant(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = TenantState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.TenantManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the data record.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item.
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing updated tenants.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Tenant existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // the avatar dialog posts its picture inline as a data url, which the binder would
            // hand to RestValueConverterImageIcon and collapse to the URI "http:///" - while the
            // request still answers 200. So it is taken out of the payload before the binder sees
            // it, decoded, and written to the icons directory. See RestApiCrudFormDataAvatarExtensions.
            var avatarSent = payload.Detach(nameof(Model.Entities.Tenant.Icon), out var avatar);

            var res = base.Update(existingItem, payload, request);

            if (avatarSent)
            {
                existingItem.Icon = RestApiCrudFormDataAvatarExtensions.Resolve
                (
                    existingItem.Id,
                    avatar,
                    existingItem.Icon,
                    () => CoreHub.GenerateIcon(existingItem.Id)
                );
            }

            CoreHub.TenantManager.Update(existingItem);

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
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Tenant existingItem, IRequest request)
        {
            CoreHub.TenantManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
