using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing fields, including adding, retrieving, and removing, as well as
    /// handling field-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing fields and events for tracking changes 
    /// to the field collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public sealed class FieldManager : IFieldManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an field is added.
        /// </summary>
        public event EventHandler<Field> FieldAdded;

        /// <summary>
        /// An event that fires when an field is udpated.
        /// </summary>
        public event EventHandler<Field> FieldUpdated;

        /// <summary>
        /// An event that fires when an field is removed.
        /// </summary>
        public event EventHandler<Field> FieldRemoved;

        /// <summary>
        /// Returns the collection of field names that are reserved and cannot be used for custom fields.
        /// </summary>
        /// <remarks>
        /// The reserved names typically represent system-defined routes and are not available
        /// for user-defined or custom field creation.
        /// </remarks>
        public static IEnumerable<string> ReservedFieldNames =>
        [
            "default", "admin", "system", "assets", "api", "workspace",
            "workspaces", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private FieldManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a field based on its id.
        /// </summary>
        /// <param name="fieldId">The id of the field.</param>
        /// <returns>The field.</returns>
        public Field GetField(Guid fieldId)
        {
            return RequestReadCache.Get(nameof(GetField), fieldId, () =>
            {
                var query = new Query<Field>()
                    .Where(x => x.Id == fieldId)
                    .WithPaging(0, 1);

                return ModelHub.GetFields(query)
                    .FirstOrDefault();
            }, typeof(Field));
        }

        /// <summary>
        /// Returns a field based on its id.
        /// </summary>
        /// <param name="fieldId">The id of the field.</param>
        /// <returns>The field.</returns>
        public Field GetField(FieldIdParameter fieldId)
        {
            var guid = Guid.TryParse(fieldId.Value, out Guid id) ? id : Guid.Empty;

            return GetField(guid);
        }

        /// <summary>
        /// Retrieves a collection of fields that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// An enumerable collection of fields that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Field> GetFields(ClassIdParameter classId)
        {
            var guid = Guid.TryParse(classId.Value, out Guid id) ? id : Guid.Empty;

            return RequestReadCache.Get<IReadOnlyList<Field>>(nameof(GetFields), guid, () =>
            {
                var query = new Query<Field>()
                    .WhereEquals(x => x.ClassId, guid);

                return [.. ModelHub.GetFields(query)];
            }, typeof(Field));
        }

        /// <summary>
        /// Retrieves a collection of fields that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned fields. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of fields that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Field> GetFields(IQuery<Field> query)
        {
            return ModelHub.GetFields(query);
        }

        /// <summary>
        /// Retrieves a collection of fields that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned fields. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of fields that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Field> GetFields(IQuery<Field> query, IQueryContext context)
        {
            return ModelHub.GetFields(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a field to the manager.
        /// </summary>
        /// <param name="fieldEntity">The field to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IFieldManager Add(Field fieldEntity)
        {
            ArgumentNullException.ThrowIfNull(fieldEntity);

            ModelHub.Add(fieldEntity);

            FieldAdded?.Invoke(this, fieldEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.field.created", fieldEntity);

            return this;
        }

        /// <summary>
        /// Update a field to the manager.
        /// </summary>
        /// <param name="fieldEntity">The field to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IFieldManager Update(Field fieldEntity)
        {
            ArgumentNullException.ThrowIfNull(fieldEntity);

            ModelHub.Update(fieldEntity);

            FieldUpdated?.Invoke(this, fieldEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.field.updated", fieldEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified field from the manager.
        /// </summary>
        /// <remarks>This method removes the specified field from the manager. If the field does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="fieldId">The field id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IFieldManager Remove(Guid fieldId)
        {
            var fieldEntry = GetField(fieldId);

            if (fieldEntry is not null)
            {
                ModelHub.Remove(fieldEntry);
                FieldRemoved?.Invoke(this, fieldEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
