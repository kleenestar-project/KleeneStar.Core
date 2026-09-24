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

namespace KleeneStar.Core.WWW.Api._1_.Slas
{
    /// <summary>
    /// Provides CRUD operations for SLA-policy items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : global::KleeneStar.Core.WebRestApi.RestApiCrudClassStructure<SlaPolicy>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the class a record belongs to, which decides who may change it.
        /// </summary>
        /// <param name="item">The record.</param>
        /// <returns>The class id.</returns>
        protected override System.Guid? ClassOf(SlaPolicy item)
        {
            return item?.ClassId;
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
        protected override IEnumerable<SlaPolicy> Retrieve(IQuery<SlaPolicy> query, IQueryContext context, IRequest request)
        {
            return CoreHub.SlaManager.GetSlas(query, context);
        }

        /// <summary>
        /// Retrieves the data required to create a new sla entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new sla for creation.
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
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<SlaPolicy> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SlaManager.GetSlas(query, context).FirstOrDefault();

            if (data is null)
            {
                return RetrieveForClone(request, new SlaPolicy());
            }

            var newItem = new SlaPolicy
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                ClassId = data.ClassId,
                State = SlaPolicyState.Draft,
                Priority = data.Priority,
                Calendar = data.Calendar,
                Notifications = data.Notifications,
                PauseOn = data.PauseOn,
                OwnerId = data.OwnerId
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a sla identified by the specified key for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the sla associated with the specified key.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<SlaPolicy> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SlaManager.GetSlas(query, context).FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the sla entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the sla entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<SlaPolicy> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SlaManager.GetSlas(query, context).FirstOrDefault();

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
        /// The dynamic payload containing updated fields.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        /// <returns>
        /// An IRestApiValidationResult indicating validation success or errors.
        /// </returns>
        protected override IRestApiValidationResult Validate(SlaPolicy existingItem, RestApiCrudFormData payload, IRequest request)
        {
            return base.Validate(existingItem, payload, request);
        }

        /// <summary>
        /// Persists a newly created SLA policy. Generates a fresh GUID and icon, defaults
        /// the state to <see cref="SlaPolicyState.Draft"/> (so the policy is not enforced
        /// until the author explicitly activates it), then binds the supplied form values.
        /// </summary>
        /// <param name="fieldMap">
        /// The dynamic form payload containing the values for the new policy.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly created policy.
        /// </param>
        /// <returns>A result object describing the create operation.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out SlaPolicy newItem)
        {
            var id = Guid.NewGuid();
            newItem = new SlaPolicy(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = SlaPolicyState.Draft
            };

            fieldMap.BindTo(newItem);

            CoreHub.SlaManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new SLA policy by cloning the supplied existing entry. Copies the
        /// class id, priority, calendar reference, notification channels, pause-on
        /// statuses, and owner from the original; resets the state to
        /// <see cref="SlaPolicyState.Draft"/> so the clone is not immediately enforced.
        /// </summary>
        /// <param name="existingItem">
        /// The policy to clone from. Its child collections (targets, scope, escalations)
        /// are handled separately by the form payload.
        /// </param>
        /// <param name="fieldMap">
        /// The dynamic form payload containing the values for the cloned policy.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly cloned policy.
        /// </param>
        /// <returns>A result object describing the clone operation.</returns>
        protected override IRestApiCrudResultCreate Clone(SlaPolicy existingItem, RestApiCrudFormData fieldMap, IRequest request, out SlaPolicy newItem)
        {
            var id = Guid.NewGuid();
            newItem = new SlaPolicy(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = SlaPolicyState.Draft,
                ClassId = existingItem.ClassId,
                Priority = existingItem.Priority,
                Calendar = existingItem.Calendar,
                Notifications = existingItem.Notifications,
                PauseOn = existingItem.PauseOn,
                OwnerId = existingItem.OwnerId
            };

            fieldMap.BindTo(newItem);

            CoreHub.SlaManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the supplied existing policy with the values from the form payload
        /// and persists the change through the <see cref="CoreHub.SlaManager"/>.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted policy being updated.
        /// </param>
        /// <param name="payload">
        /// The dynamic form payload containing the new values.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <returns>A result object describing the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update(SlaPolicy existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            CoreHub.SlaManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the supplied policy via the <see cref="CoreHub.SlaManager"/>.
        /// The cascade configured on the EF Core relationships also removes the
        /// dependent <see cref="SlaTarget"/>, <see cref="SlaScopeRule"/>, and
        /// <see cref="SlaEscalationLevel"/> rows.
        /// </summary>
        /// <param name="existingItem">
        /// The policy to delete.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <returns>A result object describing the delete operation.</returns>
        protected override IRestApiCrudResultDelete Delete(SlaPolicy existingItem, IRequest request)
        {
            CoreHub.SlaManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
