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
    /// Manages the lifecycle of <see cref="Value"/> entities — the per-object per-field
    /// payloads that back the typed inputs in the object detail and edit views. The
    /// manager intentionally returns the persisted <see cref="Value.Data"/> string as
    /// is; type-specific marshalling (parsing dates, splitting tag lists, etc.) is the
    /// responsibility of the consumer because it depends on the
    /// <see cref="Field.FieldType"/> the value is bound to.
    /// </summary>
    /// <remarks>
    /// The manager owns no write path of its own. Every mutation is handed to the
    /// <see cref="ICommitManager"/>, which writes the value row and the commit describing it in
    /// one transaction — that is what makes it impossible for a field to change without the
    /// history saying so. The public surface is unchanged: a write still takes effect as far as
    /// every reader is concerned, because a write staged by an open commit scope is merged into
    /// the object-scoped reads below.
    /// </remarks>
    public sealed class ValueManager : IValueManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a value has been added via <see cref="Add(Value)"/>.
        /// </summary>
        public event EventHandler<Value> ValueAdded;

        /// <summary>
        /// Raised after a value's payload has been updated via <see cref="Update(Value)"/>.
        /// </summary>
        public event EventHandler<Value> ValueUpdated;

        /// <summary>
        /// Raised after a value has been removed via <see cref="Remove(Guid)"/>.
        /// </summary>
        public event EventHandler<Value> ValueRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ValueManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the value identified by the supplied id.
        /// </summary>
        /// <param name="valueId">The value id.</param>
        /// <returns>The value, or <c>null</c> when no entry matches.</returns>
        public Value GetValue(Guid valueId)
        {
            var query = new Query<Value>()
                .Where(x => x.Id == valueId)
                .WithPaging(0, 1);

            return ModelHub.GetValues(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the value attached to the supplied (object, field) pair.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="fieldId">The field id.</param>
        /// <returns>The value, or <c>null</c> when no entry matches.</returns>
        public Value GetValue(Guid objectId, Guid fieldId)
        {
            // a write made inside an open commit scope is not in the store yet, but the code that
            // made it must still read it back as its own
            if (CoreHub.CommitManager is CommitManager commitManager &&
                commitManager.TryGetStagedValue(objectId, fieldId, out var staged))
            {
                return staged;
            }

            var query = new Query<Value>()
                .Where(x => x.ObjectId == objectId && x.FieldId == fieldId)
                .WithPaging(0, 1);

            return ModelHub.GetValues(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the payloads of the supplied fields for every supplied object in one read.
        /// </summary>
        /// <param name="objectIds">The ids of the objects.</param>
        /// <param name="fieldIds">The ids of the fields.</param>
        /// <returns>
        /// The payload per (object, field) pair. A pair without a value is not in the map.
        /// </returns>
        public IReadOnlyDictionary<(Guid ObjectId, Guid FieldId), string> GetData(IEnumerable<Guid> objectIds, IEnumerable<Guid> fieldIds)
        {
            var objects = objectIds?.Distinct().ToList() ?? [];
            var fields = fieldIds?.Distinct().ToList() ?? [];
            var result = new Dictionary<(Guid ObjectId, Guid FieldId), string>();

            foreach (var (objectId, fieldId, data) in ModelHub.GetValueData(objects, fields))
            {
                result.TryAdd((objectId, fieldId), data);
            }

            // a write made inside an open commit scope is not in the store yet, but the code that
            // made it must still read it back as its own
            if (CoreHub.CommitManager is CommitManager commitManager)
            {
                foreach (var objectId in objects)
                {
                    foreach (var fieldId in fields)
                    {
                        if (commitManager.TryGetStagedValue(objectId, fieldId, out var staged))
                        {
                            if (staged is null)
                            {
                                result.Remove((objectId, fieldId));
                            }
                            else
                            {
                                result[(objectId, fieldId)] = staged.Data;
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns every value attached to the object with the supplied id.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The values attached to the object. The collection may be empty.</returns>
        public IEnumerable<Value> GetValues(Guid objectId)
        {
            var query = new Query<Value>()
                .WhereEquals(x => x.ObjectId, objectId);

            var persisted = ModelHub.GetValues(query).ToList();

            return CoreHub.CommitManager is CommitManager commitManager
                ? [.. commitManager.OverlayValues(objectId, persisted)]
                : persisted;
        }

        /// <summary>
        /// Returns the values that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching values.</returns>
        public IEnumerable<Value> GetValues(IQuery<Value> query)
        {
            return ModelHub.GetValues(query);
        }

        /// <summary>
        /// Returns the values that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching values.</returns>
        public IEnumerable<Value> GetValues(IQuery<Value> query, IQueryContext context)
        {
            return ModelHub.GetValues(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Records the supplied value as a change on its object and raises
        /// <see cref="ValueAdded"/>. Returns the manager instance to allow chaining. Values are
        /// sub-resources of an object save and intentionally do not emit their own UI
        /// notification — the owning object's create/update notification already covers the
        /// operation.
        /// </summary>
        /// <remarks>
        /// The row is written by the <see cref="ICommitManager"/> together with the commit that
        /// describes it: inside an open scope when the caller opened one, and as a commit of its
        /// own otherwise. A write that carries the payload the field already holds is dropped,
        /// so an unchanged save does not appear in the history as an edit.
        /// </remarks>
        /// <param name="value">The value to add.</param>
        /// <returns>The current manager instance.</returns>
        public IValueManager Add(Value value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Id == Guid.Empty)
            {
                value.Id = Guid.NewGuid();
            }

            var previous = GetValue(value.ObjectId, value.FieldId)?.Data;

            Commits().StageWrite(value, previous, Guid.Empty);

            ValueAdded?.Invoke(this, value);

            return this;
        }

        /// <summary>
        /// Records the supplied value's payload change on its object. Raises
        /// <see cref="ValueUpdated"/>. See the remarks on <see cref="Add(Value)"/> for how and
        /// when the row reaches the store.
        /// </summary>
        /// <param name="value">The value to update.</param>
        /// <returns>The current manager instance.</returns>
        public IValueManager Update(Value value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var previous = GetValue(value.ObjectId, value.FieldId)?.Data;

            Commits().StageWrite(value, previous, Guid.Empty);

            ValueUpdated?.Invoke(this, value);

            return this;
        }

        /// <summary>
        /// Records the removal of the value identified by the supplied id. Raises
        /// <see cref="ValueRemoved"/>. No-op when the value does not exist.
        /// </summary>
        /// <param name="valueId">The value id.</param>
        /// <returns>The current manager instance.</returns>
        public IValueManager Remove(Guid valueId)
        {
            var existing = GetValue(valueId);

            if (existing is not null)
            {
                Commits().StageRemoval(existing, Guid.Empty);
                ValueRemoved?.Invoke(this, existing);
            }

            return this;
        }

        /// <summary>
        /// Returns the commit manager the writes are routed through.
        /// </summary>
        /// <returns>The commit manager.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the commit manager is not available. A value write that cannot be recorded
        /// must fail rather than quietly leave a gap in the history.
        /// </exception>
        private static CommitManager Commits()
        {
            return CoreHub.CommitManager as CommitManager
                ?? throw new InvalidOperationException("The commit manager is not available; value writes cannot be recorded.");
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
