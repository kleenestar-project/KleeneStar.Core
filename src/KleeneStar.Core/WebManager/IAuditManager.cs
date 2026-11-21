using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for the audit log: the installation-wide, append-only, hash-chained
    /// record of what happened, who caused it, and what it changed.
    /// </summary>
    /// <remarks>
    /// The interface exposes no way to modify or delete an event. That is not an oversight -
    /// it is the point. The only mutation the log offers is <see cref="Prune"/>, which enforces
    /// a retention horizon and records its own execution.
    /// </remarks>
    public interface IAuditManager : IComponentManager
    {
        /// <summary>
        /// An event that fires after an event has been appended to the log.
        /// </summary>
        event EventHandler<AuditEvent> AuditEventRecorded;

        /// <summary>
        /// Gets every event the log holds, newest first.
        /// </summary>
        IEnumerable<AuditEvent> Events { get; }

        /// <summary>
        /// Gets the number of events the log holds.
        /// </summary>
        long Count { get; }

        /// <summary>
        /// Subscribes the log to the managers whose changes it records. Idempotent.
        /// </summary>
        void Connect();

        /// <summary>
        /// Opens the context one action is audited under, or joins the one already open.
        /// </summary>
        /// <param name="origin">What is setting the action in motion.</param>
        /// <param name="actorId">The identity responsible, or <see cref="Guid.Empty"/>.</param>
        /// <param name="agent">
        /// The stable name of the non-human party acting, or <see langword="null"/> for a person.
        /// </param>
        /// <param name="clientAddress">The address the request arrived from, or <see langword="null"/>.</param>
        /// <returns>The activity. Dispose it to restore the previous context.</returns>
        IAuditActivity BeginActivity(AuditOrigin origin, Guid actorId, string agent = null, string clientAddress = null);

        /// <summary>
        /// Appends an event to the log.
        /// </summary>
        /// <param name="category">The functional area the event belongs to.</param>
        /// <param name="action">What was done.</param>
        /// <param name="target">The record it was done to.</param>
        /// <param name="deltas">The state changes it produced. May be <see langword="null"/>.</param>
        /// <param name="outcome">Whether it took effect.</param>
        /// <param name="severity">How much attention it warrants.</param>
        /// <returns>The appended event, or <see langword="null"/> when it could not be written.</returns>
        AuditEvent Record
        (
            AuditCategory category,
            AuditAction action,
            AuditTarget target,
            IEnumerable<AuditDelta> deltas = null,
            AuditOutcome outcome = AuditOutcome.Succeeded,
            AuditSeverity severity = AuditSeverity.Info
        );

        /// <summary>
        /// Appends an event describing what an action did to a record, deriving the deltas from
        /// the record itself.
        /// </summary>
        /// <remarks>
        /// For a creation or a deletion the deltas are every attribute the record holds. For a
        /// modification they are the difference between the record and what the log last knew
        /// about it, so the event states the change rather than restating the record.
        /// </remarks>
        /// <param name="category">The functional area the event belongs to.</param>
        /// <param name="action">What was done.</param>
        /// <param name="entity">The record it was done to.</param>
        /// <param name="severity">How much attention it warrants.</param>
        /// <returns>The appended event, or <see langword="null"/> when it could not be written.</returns>
        AuditEvent RecordChange(AuditCategory category, AuditAction action, object entity, AuditSeverity severity = AuditSeverity.Info);

        /// <summary>
        /// Returns the events that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching events.</returns>
        IEnumerable<AuditEvent> GetEvents(IQuery<AuditEvent> query);

        /// <summary>
        /// Returns the events that satisfy the supplied query, executed inside the supplied
        /// query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching events.</returns>
        IEnumerable<AuditEvent> GetEvents(IQuery<AuditEvent> query, IQueryContext context);

        /// <summary>
        /// Returns how many events satisfy the supplied query without loading them. This is
        /// the filtered counterpart of <see cref="Count"/>, which reports the length of the
        /// whole chain: a caller asking "how much happened lately" filters by
        /// <see cref="AuditEvent.Timestamp"/> and counts that.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted events. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching events.</returns>
        int CountEvents(IQuery<AuditEvent> query);

        /// <summary>
        /// Returns a single event by its unique identifier.
        /// </summary>
        /// <param name="eventId">The event id.</param>
        /// <returns>The event, or <see langword="null"/>.</returns>
        AuditEvent GetEvent(Guid eventId);

        /// <summary>
        /// Returns a single event by its position in the sequence.
        /// </summary>
        /// <param name="sequence">The 1-based position.</param>
        /// <returns>The event, or <see langword="null"/>.</returns>
        AuditEvent GetEvent(long sequence);

        /// <summary>
        /// Returns every event recorded about one record, oldest first.
        /// </summary>
        /// <param name="targetType">The kind of record.</param>
        /// <param name="targetId">The durable id of the record.</param>
        /// <returns>The trail, oldest first.</returns>
        IEnumerable<AuditEvent> GetTrail(AuditTargetType targetType, Guid targetId);

        /// <summary>
        /// Returns every event of one activity, oldest first.
        /// </summary>
        /// <param name="correlationId">The correlation shared by the events.</param>
        /// <returns>The events, oldest first.</returns>
        IEnumerable<AuditEvent> GetActivity(Guid correlationId);

        /// <summary>
        /// Reconstructs the state of one record as the log knows it, at a point in the sequence.
        /// </summary>
        /// <param name="targetType">The kind of record.</param>
        /// <param name="targetId">The durable id of the record.</param>
        /// <param name="atSequence">
        /// The position to replay up to, or 0 for the whole trail.
        /// </param>
        /// <returns>The projection. Never <see langword="null"/>.</returns>
        AuditProjection Project(AuditTargetType targetType, Guid targetId, long atSequence = 0);

        /// <summary>
        /// Walks the hash chain and reports whether the log is intact.
        /// </summary>
        /// <param name="fromSequence">The position to anchor at, or 0 for the oldest event held.</param>
        /// <param name="count">The largest number of events to check, or 0 for all of them.</param>
        /// <returns>The result.</returns>
        AuditVerification Verify(long fromSequence = 0, int count = 0);

        /// <summary>
        /// Removes every event recorded before the supplied moment and records that it did.
        /// </summary>
        /// <param name="before">The retention horizon.</param>
        /// <param name="actorId">The identity ordering the retention run.</param>
        /// <returns>The number of events removed.</returns>
        int Prune(DateTime before, Guid actorId);
    }
}
