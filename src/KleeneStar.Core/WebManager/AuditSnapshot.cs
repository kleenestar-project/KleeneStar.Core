using KleeneStar.Model.Attributes;
using KleeneStar.Model.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Turns a record into the set of typed attribute values the audit log stores, and turns two
    /// such sets into the deltas between them.
    /// </summary>
    /// <remarks>
    /// This is where the "no raw blobs" rule is enforced. Serializing an entity to JSON and
    /// storing the document would be a great deal less code, and would produce a log that cannot
    /// be filtered by attribute, cannot be diffed without a parser that knows the schema of the
    /// day it was written, and cannot answer "when did this field last change" at all. A
    /// snapshot is therefore a flat set of named, individually typed values, and every value is
    /// serialized with the invariant culture so the same state always produces the same text.
    /// <para>
    /// Only scalars are taken. Navigation properties and collections of entities are skipped
    /// rather than flattened: the related records audit themselves, and following the graph
    /// would record the same change from several directions at once.
    /// </para>
    /// </remarks>
    public static class AuditSnapshot
    {
        /// <summary>
        /// The property every entity carries for the store's own use, which says nothing about
        /// the state of the record and differs between a database and its restored copy.
        /// </summary>
        private const string StoreKeyProperty = "RawId";

        /// <summary>
        /// The readable scalar properties of each audited type, resolved once per type.
        /// </summary>
        private static readonly Dictionary<Type, PropertyInfo[]> _properties = [];

        /// <summary>
        /// Guards <see cref="_properties"/>, which is filled from whichever request first
        /// audits a type.
        /// </summary>
        private static readonly object _gate = new();

        /// <summary>
        /// Reads the attribute values a record currently holds.
        /// </summary>
        /// <param name="entity">The record. May be <c>null</c>.</param>
        /// <returns>
        /// The values, keyed by the lower-case attribute name. Attributes holding nothing are
        /// omitted, so an absent key means "the record has no value here" rather than "the
        /// snapshot did not look".
        /// </returns>
        public static IReadOnlyDictionary<string, AuditValue> Capture(object entity)
        {
            var values = new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

            if (entity is null)
            {
                return values;
            }

            foreach (var property in Properties(entity.GetType()))
            {
                var name = property.Name.ToLowerInvariant();

                if (property.IsDefined(typeof(AuditRedactedAttribute), true))
                {
                    // a secret still produces an entry, so that changing it is auditable; what
                    // it changed to is not recoverable from the log
                    var held = Read(property, entity);

                    if (held is not null)
                    {
                        values[name] = new AuditValue(AuditValueKindExtensions.RedactedMarker, AuditValueKind.Redacted);
                    }

                    continue;
                }

                var value = Serialize(Read(property, entity), property.PropertyType);

                if (value is not null)
                {
                    values[name] = value;
                }
            }

            return values;
        }

        /// <summary>
        /// Produces the deltas that carry a record from one set of attribute values to another.
        /// </summary>
        /// <remarks>
        /// The three kinds are decided here and stored, never re-derived later: an attribute
        /// present only in <paramref name="after"/> was added, one present only in
        /// <paramref name="before"/> was removed, and one present in both with a different
        /// payload was modified. An attribute present in both with the same payload produces no
        /// delta at all - a log that recorded unchanged attributes would grow with the width of
        /// the record rather than with the size of the change, and the deltas would stop being a
        /// description of what happened.
        /// </remarks>
        /// <param name="before">The values the record held. May be <c>null</c> or empty.</param>
        /// <param name="after">The values it holds now. May be <c>null</c> or empty.</param>
        /// <returns>The deltas, ordered by attribute name so a diff is stable between runs.</returns>
        public static IReadOnlyList<AuditDelta> Diff(IReadOnlyDictionary<string, AuditValue> before, IReadOnlyDictionary<string, AuditValue> after)
        {
            var previous = before ?? new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);
            var current = after ?? new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

            var names = previous.Keys
                .Union(current.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

            var deltas = new List<AuditDelta>();

            foreach (var name in names)
            {
                var had = previous.TryGetValue(name, out var old);
                var has = current.TryGetValue(name, out var @new);

                if (had && has)
                {
                    if (!string.Equals(old.Value, @new.Value, StringComparison.Ordinal))
                    {
                        deltas.Add(AuditDelta.Modified(name, old.Value, @new.Value, @new.Kind));
                    }

                    continue;
                }

                if (has)
                {
                    deltas.Add(AuditDelta.Added(name, @new.Value, @new.Kind));

                    continue;
                }

                deltas.Add(AuditDelta.Removed(name, old.Value, old.Kind));
            }

            return deltas;
        }

        /// <summary>
        /// Produces the deltas that record a record entering or leaving the log in one piece:
        /// every attribute it holds, all of the same kind.
        /// </summary>
        /// <param name="entity">The record.</param>
        /// <param name="kind">
        /// <see cref="AuditDeltaKind.Added"/> for a creation, <see cref="AuditDeltaKind.Removed"/>
        /// for a deletion.
        /// </param>
        /// <returns>The deltas, ordered by attribute name.</returns>
        public static IReadOnlyList<AuditDelta> Describe(object entity, AuditDeltaKind kind)
        {
            var values = Capture(entity);

            return [.. values
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => kind == AuditDeltaKind.Removed
                    ? AuditDelta.Removed(x.Key, x.Value.Value, x.Value.Kind)
                    : AuditDelta.Added(x.Key, x.Value.Value, x.Value.Kind))];
        }

        /// <summary>
        /// Serializes one value into its audit payload and the kind that says how to read it
        /// back.
        /// </summary>
        /// <remarks>
        /// Enumerations are recorded by member name rather than by ordinal. An ordinal is only
        /// meaningful against the version of the enumeration that wrote it, so inserting a
        /// member would silently change what every past event said.
        /// </remarks>
        /// <param name="value">The value. May be <c>null</c>.</param>
        /// <param name="declared">The declared type of the property it came from.</param>
        /// <returns>The payload, or <c>null</c> when the value carries nothing.</returns>
        private static AuditValue Serialize(object value, Type declared)
        {
            if (value is null)
            {
                return null;
            }

            var type = Nullable.GetUnderlyingType(declared) ?? declared;

            if (type.IsEnum)
            {
                return new AuditValue(Enum.GetName(type, value) ?? value.ToString(), AuditValueKind.Enumeration);
            }

            return value switch
            {
                string x => string.IsNullOrEmpty(x) ? null : new AuditValue(x, AuditValueKind.Text),
                bool x => new AuditValue(x ? "true" : "false", AuditValueKind.Boolean),
                Guid x => x == Guid.Empty ? null : new AuditValue(x.ToString("D", CultureInfo.InvariantCulture), AuditValueKind.Reference),
                DateTime x => new AuditValue(x.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), AuditValueKind.Timestamp),
                DateTimeOffset x => new AuditValue(x.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), AuditValueKind.Timestamp),
                TimeSpan x => new AuditValue(x.ToString("c", CultureInfo.InvariantCulture), AuditValueKind.Text),
                IFormattable x when IsNumeric(type) => new AuditValue(x.ToString(null, CultureInfo.InvariantCulture), AuditValueKind.Number),
                _ => null
            };
        }

        /// <summary>
        /// Reads a property, treating a failure as an absent value rather than as a reason to
        /// abandon the snapshot.
        /// </summary>
        /// <remarks>
        /// A computed property may throw when the graph around it is not loaded. Losing that one
        /// attribute is a small hole in one event; losing the event is a hole in the trail.
        /// </remarks>
        /// <param name="property">The property.</param>
        /// <param name="entity">The record.</param>
        /// <returns>The value, or <c>null</c>.</returns>
        private static object Read(PropertyInfo property, object entity)
        {
            try
            {
                return property.GetValue(entity);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns whether a type is one of the numeric primitives the log records as numbers.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns><c>true</c> for an integral or floating-point type.</returns>
        private static bool IsNumeric(Type type)
        {
            return Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte
                or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
        }

        /// <summary>
        /// Returns the scalar properties of a type that the log records, resolved once and
        /// cached.
        /// </summary>
        /// <param name="type">The entity type.</param>
        /// <returns>The properties, in declaration order.</returns>
        private static PropertyInfo[] Properties(Type type)
        {
            lock (_gate)
            {
                if (_properties.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                var resolved = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.CanRead)
                    .Where(x => x.GetIndexParameters().Length == 0)
                    .Where(x => !string.Equals(x.Name, StoreKeyProperty, StringComparison.Ordinal))
                    .Where(x => !x.IsDefined(typeof(AuditIgnoreAttribute), true))
                    .Where(x => IsScalar(x.PropertyType))
                    .ToArray();

                _properties[type] = resolved;

                return resolved;
            }
        }

        /// <summary>
        /// Returns whether a property type is a scalar the log records, as opposed to a
        /// navigation property or a collection.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <returns><c>true</c> when the value is recorded.</returns>
        private static bool IsScalar(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying.IsEnum || underlying.IsPrimitive)
            {
                return true;
            }

            if (underlying == typeof(string) || underlying == typeof(Guid) || underlying == typeof(decimal)
                || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(TimeSpan))
            {
                return true;
            }

            // a collection or another entity: the related records audit themselves
            return !typeof(IEnumerable).IsAssignableFrom(underlying) && underlying.IsValueType;
        }
    }

    /// <summary>
    /// One serialized attribute value together with the kind that says how to read it back.
    /// </summary>
    /// <param name="Value">The serialized payload.</param>
    /// <param name="Kind">How the payload is to be read back.</param>
    public sealed record AuditValue(string Value, AuditValueKind Kind);
}
