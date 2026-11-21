using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The per-class definitions an object overview table needs to draw the cells of one
    /// class: its fields and the fixed value sets its status and priority fields offer.
    /// </summary>
    internal sealed class ObjectTableClassContext
    {
        /// <summary>Gets the class the context describes.</summary>
        public Class Class { get; init; }

        /// <summary>Gets the maintainable fields of the class.</summary>
        public IReadOnlyList<Field> Fields { get; init; } = [];

        /// <summary>Gets the active statuses of the class.</summary>
        public IReadOnlyList<Status> Statuses { get; init; } = [];

        /// <summary>Gets the active priorities of the class.</summary>
        public IReadOnlyList<Priority> Priorities { get; init; } = [];

        /// <summary>
        /// Reads the class definitions from the managers.
        /// </summary>
        /// <param name="cls">The class to describe.</param>
        /// <returns>The context.</returns>
        public static ObjectTableClassContext Build(Class cls)
        {
            return new ObjectTableClassContext
            {
                Class = cls,
                Fields =
                [
                    .. CoreHub.FieldManager
                        .GetFields(new ClassIdParameter(cls.Id))
                        .Where(f => !f.Deprecated && f.State == FieldState.Active)
                ],
                Statuses =
                [
                    .. CoreHub.StatusManager
                        .GetStatuses(new ClassIdParameter(cls.Id))
                        .Where(s => s.State == StatusState.Active)
                ],
                Priorities =
                [
                    .. CoreHub.PriorityManager
                        .GetPriorities(new ClassIdParameter(cls.Id))
                        .Where(p => p.State == PriorityState.Active)
                ]
            };
        }
    }

    /// <summary>
    /// The data an object overview table needs to fill the cells of the page it is about
    /// to answer with: the class definitions of the objects on that page and their
    /// stored field values.
    /// </summary>
    /// <remarks>
    /// Both are loaded once for the whole page rather than per cell. A page of fifty
    /// issues shown with twenty field columns asks for a thousand values; read one at a
    /// time that is a thousand round trips, and every one of them opens its own database
    /// context.
    /// </remarks>
    internal sealed class ObjectTableProjection
    {
        private readonly IReadOnlyDictionary<Guid, ObjectTableClassContext> _classes;
        private readonly IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, string>> _values;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        private ObjectTableProjection
        (
            IReadOnlyDictionary<Guid, ObjectTableClassContext> classes,
            IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, string>> values
        )
        {
            _classes = classes;
            _values = values;
        }

        /// <summary>
        /// Loads the class definitions and the stored field values of the supplied
        /// objects.
        /// </summary>
        /// <param name="objects">The objects of the page being answered.</param>
        /// <returns>The projection.</returns>
        public static ObjectTableProjection Build(IReadOnlyCollection<ObjectEntity> objects)
        {
            var classes = new Dictionary<Guid, ObjectTableClassContext>();

            foreach (var classId in objects.Select(x => x.ClassId).Distinct())
            {
                var cls = CoreHub.ClassManager.GetClass(classId);

                if (cls is not null)
                {
                    classes[classId] = ObjectTableClassContext.Build(cls);
                }
            }

            // the values of every object are read in one query rather than one per object.
            // The per-object call opens a database connection of its own, so a page of
            // fifty rows opened fifty connections and a sort over the whole set one per
            // object in scope — enough churn to collide with any concurrent write and fail
            // the request with "database is locked".
            var ids = objects.Select(x => x.Id).ToHashSet();

            var values = ids.ToDictionary
            (
                id => id,
                _ => (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>()
            );

            if (ids.Count > 0)
            {
                var query = new Query<Value>()
                    .WhereEquals(x => ids.Contains(x.ObjectId));

                foreach (var group in CoreHub.ValueManager.GetValues(query).GroupBy(x => x.ObjectId))
                {
                    values[group.Key] = group
                        .GroupBy(v => v.FieldId)
                        .ToDictionary(g => g.Key, g => g.First().Data);
                }
            }

            return new ObjectTableProjection(classes, values);
        }

        /// <summary>
        /// Returns the class context of an object, or null when its class is gone.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The class context, or null.</returns>
        public ObjectTableClassContext ResolveClass(ObjectEntity @object)
        {
            return @object is not null && _classes.TryGetValue(@object.ClassId, out var context)
                ? context
                : null;
        }

        /// <summary>
        /// Returns whether the object's class defines one of the supplied fields, which is
        /// what decides whether the object can hold a value for the column that folded
        /// them.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="fieldIds">The field ids the column stands for.</param>
        /// <returns><see langword="true"/> when the object's class defines one of them.</returns>
        public bool DefinesField(ObjectEntity @object, IReadOnlySet<Guid> fieldIds)
        {
            var context = ResolveClass(@object);

            return context is not null && context.Fields.Any(f => fieldIds.Contains(f.Id));
        }

        /// <summary>
        /// Reads an object's value for a field column: the value of whichever of the
        /// column's folded fields the object's class defines.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="fieldIds">The field ids the column stands for.</param>
        /// <param name="fieldType">The field type, which decides the cell format.</param>
        /// <returns>The cell content, or null when the object has no value.</returns>
        public string ReadFieldValue(ObjectEntity @object, IReadOnlySet<Guid> fieldIds, FieldType fieldType)
        {
            if (@object is null || !_values.TryGetValue(@object.Id, out var byField))
            {
                return null;
            }

            foreach (var fieldId in fieldIds)
            {
                if (byField.TryGetValue(fieldId, out var data) && !string.IsNullOrEmpty(data))
                {
                    return Format(data, fieldType);
                }
            }

            return null;
        }

        /// <summary>
        /// Brings a stored payload into the shape the cell renderer of its field type
        /// reads.
        /// </summary>
        /// <remarks>
        /// Only tags need it: the value row stores them comma-separated (the shape the
        /// object detail page writes), while the tag control reads and writes them
        /// semicolon-separated. Splitting on both separators keeps rows written by either
        /// side readable.
        /// </remarks>
        private static string Format(string data, FieldType fieldType)
        {
            if (fieldType != FieldType.Tag)
            {
                return data;
            }

            return string.Join(";", data
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
