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

namespace KleeneStar.Core.WWW.Api._1_.Calendars
{
    using Calendar = KleeneStar.Model.Entities.Calendar;

    /// <summary>
    /// Provides CRUD operations for calendar items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : global::KleeneStar.Core.WebRestApi.RestApiCrudClassStructure<Calendar>
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
        protected override System.Guid? ClassOf(Calendar item)
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
        protected override IEnumerable<Calendar> Retrieve(IQuery<Calendar> query, IQueryContext context, IRequest request)
        {
            return CoreHub.CalendarManager.GetCalendars(query, context);
        }

        /// <summary>
        /// Retrieves the data required to create a new calendar entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new calendar for creation.
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
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Calendar> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.CalendarManager.GetCalendars(query, context).FirstOrDefault();

            if (data is null)
            {
                return RetrieveForClone(request, new Calendar());
            }

            var newItem = new Calendar
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                TimeZone = data.TimeZone,
                Region = data.Region,
                Icon = data.Icon,
                ClassId = data.ClassId,
                State = CalendarState.Active,
                IsDefault = false
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a calendar identified by the specified key for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the calendar associated with the specified key.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Calendar> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.CalendarManager.GetCalendars(query, context).FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the calendar entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the calendar entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Calendar> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.CalendarManager.GetCalendars(query, context).FirstOrDefault();

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
        protected override IRestApiValidationResult Validate(Calendar existingItem, RestApiCrudFormData payload, IRequest request)
        {
            return base.Validate(existingItem, payload, request);
        }

        /// <summary>
        /// Persists a newly created calendar. Generates a fresh GUID and icon, defaults
        /// the state to <see cref="CalendarState.Active"/> and the time zone/region to
        /// <c>Europe/Berlin</c> / <c>DE</c>, then binds the supplied form values.
        /// </summary>
        /// <param name="fieldMap">
        /// The dynamic form payload containing the values for the new calendar.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly created calendar.
        /// </param>
        /// <returns>A result object describing the create operation.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Calendar newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Calendar(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = CalendarState.Active,
                TimeZone = "Europe/Berlin",
                Region = "DE"
            };

            fieldMap.BindTo(newItem);

            CoreHub.CalendarManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new calendar by cloning the supplied existing entry. Copies the
        /// class id, time zone, and region from the original, generates a fresh GUID
        /// and icon, then binds the supplied form values on top.
        /// </summary>
        /// <param name="existingItem">
        /// The calendar to clone from. The clone copies its class, time zone, and region.
        /// </param>
        /// <param name="fieldMap">
        /// The dynamic form payload containing the values for the cloned calendar.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly cloned calendar.
        /// </param>
        /// <returns>A result object describing the clone operation.</returns>
        protected override IRestApiCrudResultCreate Clone(Calendar existingItem, RestApiCrudFormData fieldMap, IRequest request, out Calendar newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Calendar(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = CalendarState.Active,
                ClassId = existingItem.ClassId,
                TimeZone = existingItem.TimeZone,
                Region = existingItem.Region
            };

            fieldMap.BindTo(newItem);

            CoreHub.CalendarManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the supplied existing calendar with the values from the form payload
        /// and persists the change through the <see cref="CoreHub.CalendarManager"/>.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted calendar being updated.
        /// </param>
        /// <param name="payload">
        /// The dynamic form payload containing the new values.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <returns>A result object describing the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update(Calendar existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            CoreHub.CalendarManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the supplied calendar via the <see cref="CoreHub.CalendarManager"/>.
        /// The cascade configured on the EF Core relationships also removes the
        /// dependent <see cref="BusinessHourSlot"/> and <see cref="Holiday"/> rows;
        /// any <see cref="SlaPolicy.CalendarId"/> that referenced this calendar is set
        /// to <c>null</c>.
        /// </summary>
        /// <param name="existingItem">
        /// The calendar to delete.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional operational context.
        /// </param>
        /// <returns>A result object describing the delete operation.</returns>
        protected override IRestApiCrudResultDelete Delete(Calendar existingItem, IRequest request)
        {
            CoreHub.CalendarManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
