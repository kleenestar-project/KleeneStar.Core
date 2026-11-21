using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing <see cref="Value"/> entities — the per-object
    /// per-field payloads that back the typed inputs in the object detail and edit
    /// views.
    /// </summary>
    public interface IValueManager : IComponentManager
    {
        /// <summary>
        /// Raised when a new value has been added.
        /// </summary>
        event EventHandler<Value> ValueAdded;

        /// <summary>
        /// Raised when a value's payload has been updated.
        /// </summary>
        event EventHandler<Value> ValueUpdated;

        /// <summary>
        /// Raised when a value has been removed.
        /// </summary>
        event EventHandler<Value> ValueRemoved;

        /// <summary>
        /// Returns the value identified by the supplied id.
        /// </summary>
        /// <param name="valueId">The value id.</param>
        /// <returns>The value, or <c>null</c> when no entry matches.</returns>
        Value GetValue(Guid valueId);

        /// <summary>
        /// Returns the value attached to the supplied (object, field) pair.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="fieldId">The field id.</param>
        /// <returns>The value, or <c>null</c> when no entry matches.</returns>
        Value GetValue(Guid objectId, Guid fieldId);

        /// <summary>
        /// Returns the payloads of the supplied fields for every supplied object in one read,
        /// for a list that needs a few fields of each of its records. A write staged by an open
        /// commit scope is answered the way <see cref="GetValue(Guid, Guid)"/> answers it.
        /// </summary>
        /// <param name="objectIds">The ids of the objects.</param>
        /// <param name="fieldIds">The ids of the fields.</param>
        /// <returns>
        /// The payload per (object, field) pair. A pair without a value is not in the map.
        /// </returns>
        IReadOnlyDictionary<(Guid ObjectId, Guid FieldId), string> GetData(IEnumerable<Guid> objectIds, IEnumerable<Guid> fieldIds);

        /// <summary>
        /// Returns every value attached to the object with the supplied id, in field
        /// definition order (whatever the database returns).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The values attached to the object. The collection may be empty.</returns>
        IEnumerable<Value> GetValues(Guid objectId);

        /// <summary>
        /// Returns the values that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching values.</returns>
        IEnumerable<Value> GetValues(IQuery<Value> query);

        /// <summary>
        /// Returns the values that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/>.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching values.</returns>
        IEnumerable<Value> GetValues(IQuery<Value> query, IQueryContext context);

        /// <summary>
        /// Adds a value to the manager.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns>The current instance to allow chaining.</returns>
        IValueManager Add(Value value);

        /// <summary>
        /// Updates an existing value.
        /// </summary>
        /// <param name="value">The value to update.</param>
        /// <returns>The current instance to allow chaining.</returns>
        IValueManager Update(Value value);

        /// <summary>
        /// Removes the value identified by the supplied id from the data store.
        /// </summary>
        /// <param name="valueId">The id of the value to remove.</param>
        /// <returns>The current instance to allow chaining.</returns>
        IValueManager Remove(Guid valueId);
    }
}
