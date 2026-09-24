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

// the endpoints live in KleeneStar.Core.WWW.Api._1_.SecurityLevels, so the bare entity
// name would resolve to the namespace rather than to the type
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels
{
    /// <summary>
    /// Provides CRUD operations for security level items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : global::KleeneStar.Core.WebRestApi.RestApiCrudClassStructure<SecurityLevelEntity>
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
        protected override System.Guid? ClassOf(SecurityLevelEntity item)
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
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered set of index items, which may be empty.</returns>
        protected override IEnumerable<SecurityLevelEntity> Retrieve(IQuery<SecurityLevelEntity> query, IQueryContext context, IRequest request)
        {
            return CoreHub.SecurityLevelManager.GetSecurityLevels(query, context);
        }

        /// <summary>
        /// Retrieves a result object containing default values and metadata for cloning a item.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request.</param>
        /// <returns>The data required to initialize a new item for creation.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<SecurityLevelEntity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SecurityLevelManager.GetSecurityLevels(query, context)
                .FirstOrDefault();

            if (data is null)
            {
                return RetrieveForClone(request, null);
            }

            var newItem = new SecurityLevelEntity()
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                State = SecurityLevelState.Active,
                Rank = data.Rank,
                ClassId = data.ClassId,

                // the clearance travels with the copy - a level cloned without one would be
                // closed to everyone, which is not what "clone this" asks for. The default flag
                // does not: a class starts its objects on one level, not on two
                PermittedGroupIds = data.PermittedGroupIds is null ? [] : [.. data.PermittedGroupIds],
                IsDefault = false
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a security level for update operations.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request context.</param>
        /// <returns>The security level associated with the specified id.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<SecurityLevelEntity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SecurityLevelManager.GetSecurityLevels(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the security level identified by the specified id in preparation for deletion.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request context.</param>
        /// <returns>The entity and related information required for the delete operation.</returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<SecurityLevelEntity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.SecurityLevelManager.GetSecurityLevels(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Id.ToString());
        }

        /// <summary>
        /// Validates the payload of a create or update.
        /// </summary>
        /// <remarks>
        /// Two things a security level cannot be stored without are named here rather than left
        /// to the database: the class it belongs to, and - on a create - a name.
        /// <see cref="RestApiCrud{T}"/> validates a rule only for the properties the payload
        /// carries, which is right for an update and wrong for a create.
        /// </remarks>
        /// <param name="existingItem">The currently persisted item (null for create).</param>
        /// <param name="payload">The dynamic payload containing the submitted values.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult Validate(SecurityLevelEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request)
                .ValidateClass(payload, request, existingItem?.ClassId);

            // a create carries no persisted name to fall back on, and the base rule only checks
            // the properties the payload names
            if (existingItem is null && string.IsNullOrWhiteSpace(ReadName(payload)))
            {
                result.Add
                (
                    I18N.Translate(request, "kleenestar.core:securitylevel.name.validation.required"),
                    nameof(SecurityLevelEntity.Name),
                    "securitylevel.name.missing"
                );
            }

            return result;
        }

        /// <summary>
        /// Reads the submitted name out of the payload.
        /// </summary>
        /// <remarks>
        /// The payload parser lower-cases every property name it reads off the wire, so a lookup
        /// spelled the way the property is declared misses - silently, because a missing key is
        /// indistinguishable from an unsent field. Both spellings are tried.
        /// </remarks>
        /// <param name="payload">The submitted form data.</param>
        /// <returns>The submitted name, or <c>null</c> when the payload carries none.</returns>
        private static string ReadName(RestApiCrudFormData payload)
        {
            if (payload is null)
            {
                return null;
            }

            if (payload.TryGetValue(nameof(SecurityLevelEntity.Name).ToLowerInvariant(), out var lower))
            {
                return lower?.ToString();
            }

            return payload.TryGetValue(nameof(SecurityLevelEntity.Name), out var exact) ? exact?.ToString() : null;
        }

        /// <summary>
        /// Persists the newly created security level.
        /// </summary>
        /// <param name="fieldMap">The payload containing the fields required to create it.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <param name="newItem">Receives the newly created item.</param>
        /// <returns>The result of the create operation.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out SecurityLevelEntity newItem)
        {
            var id = Guid.NewGuid();
            newItem = new SecurityLevelEntity(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = SecurityLevelState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.SecurityLevelManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new security level from the values of an existing one.
        /// </summary>
        /// <param name="existingItem">The item the clone is based on.</param>
        /// <param name="fieldMap">The form data to bind to the new instance. Cannot be null.</param>
        /// <param name="request">The current request context.</param>
        /// <param name="newItem">Receives the newly created item.</param>
        /// <returns>The result of the create operation.</returns>
        protected override IRestApiCrudResultCreate Clone(SecurityLevelEntity existingItem, RestApiCrudFormData fieldMap, IRequest request, out SecurityLevelEntity newItem)
        {
            var id = Guid.NewGuid();
            newItem = new SecurityLevelEntity(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = SecurityLevelState.Active,
                PermittedGroupIds = existingItem?.PermittedGroupIds is null ? [] : [.. existingItem.PermittedGroupIds]
            };

            fieldMap.BindTo(newItem);

            // a clone stays in the class of its original when the payload names none
            newItem.ClassId = newItem.ClassId == Guid.Empty
                ? existingItem?.ClassId ?? Guid.Empty
                : newItem.ClassId;

            CoreHub.SecurityLevelManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the data record.
        /// </summary>
        /// <param name="existingItem">The currently persisted item.</param>
        /// <param name="payload">The dynamic payload containing updated fields.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>The result of the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update(SecurityLevelEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            CoreHub.SecurityLevelManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the specified security level, declassifying every object that carried it.
        /// </summary>
        /// <param name="existingItem">The currently persisted item that is to be deleted.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>The result of the delete operation.</returns>
        protected override IRestApiCrudResultDelete Delete(SecurityLevelEntity existingItem, IRequest request)
        {
            CoreHub.SecurityLevelManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
