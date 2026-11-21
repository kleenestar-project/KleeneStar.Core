using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The state of one record as the audit log knows it, reconstructed by replaying every delta
    /// recorded about that record up to a point in the sequence.
    /// </summary>
    /// <remarks>
    /// This is the payoff of storing deltas rather than snapshots. The log never holds the full
    /// state of anything, yet the full state at any moment is recoverable from it - which is
    /// what "reconstructable" has to mean if it is to be worth anything. It also makes the log
    /// self-checking: the projection at the head can be compared against the record itself, and
    /// a difference means the log missed a change.
    /// <para>
    /// The projection is the log's account of the record, not the record. Attributes the log
    /// declined to hold (see <see cref="Model.Attributes.AuditRedactedAttribute"/>) read as
    /// their marker, and a record whose changes predate the log projects only what it has seen
    /// since.
    /// </para>
    /// </remarks>
    public sealed class AuditProjection
    {
        /// <summary>
        /// Gets the kind of record the projection describes.
        /// </summary>
        public AuditTargetType TargetType { get; init; }

        /// <summary>
        /// Gets the durable id of the record.
        /// </summary>
        public Guid TargetId { get; init; }

        /// <summary>
        /// Gets the human-readable name the record carried at the projected point.
        /// </summary>
        public string TargetKey { get; init; }

        /// <summary>
        /// Gets the position in the sequence the projection was replayed up to.
        /// </summary>
        public long Sequence { get; init; }

        /// <summary>
        /// Gets the moment of the last event folded into the projection.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets the number of events folded into the projection.
        /// </summary>
        public int EventCount { get; init; }

        /// <summary>
        /// Gets whether the last event folded in removed the record, so the projection describes
        /// what was lost rather than what exists.
        /// </summary>
        public bool IsDeleted { get; init; }

        /// <summary>
        /// Gets the attribute values, keyed by the lower-case attribute name.
        /// </summary>
        public IReadOnlyDictionary<string, AuditValue> Values { get; init; }
            = new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the value the record held for an attribute, or <see langword="null"/> when it
        /// held none.
        /// </summary>
        /// <param name="attribute">The attribute name.</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        public AuditValue Get(string attribute)
        {
            return !string.IsNullOrWhiteSpace(attribute) && Values.TryGetValue(attribute, out var value)
                ? value
                : null;
        }

        /// <summary>
        /// Replays a trail into the state it leaves behind.
        /// </summary>
        /// <remarks>
        /// The three delta kinds are applied as what they say they are, which is the whole
        /// reason they are stored rather than inferred: an addition and a modification both
        /// write, but a removal deletes the key. Collapsing them - by treating a removal as a
        /// write of <c>null</c>, say - would leave the projection unable to distinguish an
        /// attribute that exists and is empty from one that does not exist, and the two produce
        /// different records.
        /// </remarks>
        /// <param name="targetType">The kind of record.</param>
        /// <param name="targetId">The durable id of the record.</param>
        /// <param name="trail">The events about the record, oldest first.</param>
        /// <returns>The projection. Never <see langword="null"/>.</returns>
        public static AuditProjection Replay(AuditTargetType targetType, Guid targetId, IEnumerable<AuditEvent> trail)
        {
            var events = (trail ?? []).OrderBy(x => x.Sequence).ToList();
            var values = new Dictionary<string, AuditValue>(StringComparer.OrdinalIgnoreCase);

            foreach (var @event in events)
            {
                foreach (var delta in (@event.Deltas ?? []).OrderBy(x => x.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(delta.Attribute))
                    {
                        continue;
                    }

                    switch (delta.Kind)
                    {
                        case AuditDeltaKind.Removed:
                            values.Remove(delta.Attribute);
                            break;
                        default:
                            values[delta.Attribute] = new AuditValue(delta.NewValue, delta.ValueKind);
                            break;
                    }
                }
            }

            var last = events.Count > 0 ? events[^1] : null;

            return new AuditProjection
            {
                TargetType = targetType,
                TargetId = targetId,
                TargetKey = events.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.TargetKey))?.TargetKey,
                Sequence = last?.Sequence ?? 0,
                Timestamp = last?.Timestamp ?? default,
                EventCount = events.Count,
                IsDeleted = last?.Action == AuditAction.Deleted && last.Outcome == AuditOutcome.Succeeded,
                Values = values
            };
        }
    }
}
