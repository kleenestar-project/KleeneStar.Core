using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Integrity;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Owns the audit log: appends every recorded event onto the hash chain, reconstructs the
    /// state of a record by replaying what was recorded about it, and reports whether the chain
    /// is still intact.
    /// </summary>
    /// <remarks>
    /// The manager is the only writer of the audit store, and it offers no way to change or
    /// remove what it has written. Everything it exposes beyond <see cref="Record"/> is a read:
    /// a trail, an activity, a projection, a verification. That asymmetry is the design - a log
    /// that can be corrected is a log whose contents are an opinion.
    /// <para>
    /// Recording is best-effort with respect to the action being audited: a failure to write the
    /// log never propagates into the operation that raised it. This is a deliberate trade and
    /// worth stating plainly. Failing the operation instead would make the log a single point of
    /// failure for the whole installation, and would hand anybody who can break the audit store
    /// the ability to stop all work; letting the operation proceed means a storage failure can
    /// leave a hole. The hole is detectable - the sequence is gap-free, so a missing event shows
    /// up as a break in <see cref="Verify"/> - which is the property that makes the trade
    /// acceptable.
    /// </para>
    /// <para>
    /// The ambient activity is held in an <see cref="AsyncLocal{T}"/> so it follows one request
    /// through its awaits without leaking into the requests being served beside it.
    /// </para>
    /// </remarks>
    public sealed partial class AuditManager : IAuditManager
    {
        /// <summary>
        /// The number of events read at a time while walking the chain, so verifying a long log
        /// does not require holding all of it.
        /// </summary>
        private const int VerificationPageSize = 500;

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly AsyncLocal<AuditActivity> _ambient = new();
        private readonly Lock _connectionGate = new();

        private bool _connected;

        /// <summary>
        /// An event that fires after an event has been appended to the log.
        /// </summary>
        public event EventHandler<AuditEvent> AuditEventRecorded;

        /// <summary>
        /// Gets every event the log holds, newest first.
        /// </summary>
        public IEnumerable<AuditEvent> Events
        {
            get
            {
                var query = new Query<AuditEvent>()
                    .OrderByDesc(x => x.Sequence);

                return Hydrate(ModelHub.GetAuditEvents(query));
            }
        }

        /// <summary>
        /// Gets the number of events the log holds.
        /// </summary>
        public long Count => ModelHub.GetAuditEventCount();

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AuditManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Opens the context one action is audited under, or joins the one already open.
        /// </summary>
        /// <param name="origin">What is setting the action in motion.</param>
        /// <param name="actorId">The identity responsible, or <see cref="Guid.Empty"/>.</param>
        /// <param name="agent">The non-human party acting, or <see langword="null"/>.</param>
        /// <param name="clientAddress">The address the request arrived from, or <see langword="null"/>.</param>
        /// <returns>The activity. Dispose it to restore the previous context.</returns>
        public IAuditActivity BeginActivity(AuditOrigin origin, Guid actorId, string agent = null, string clientAddress = null)
        {
            var existing = _ambient.Value;

            if (existing is not null)
            {
                existing.Enter(actorId, agent, clientAddress);

                return existing;
            }

            var activity = new AuditActivity(this, null, origin, actorId, agent, clientAddress);
            _ambient.Value = activity;

            return activity;
        }

        /// <summary>
        /// Appends an event to the log.
        /// </summary>
        /// <remarks>
        /// The event inherits its origin, actor and correlation from the ambient activity. When
        /// none is open the event is still recorded rather than dropped: it is attributed to the
        /// identity the session resolves to and classified as a user action when one resolves,
        /// and as a system action when none does. An unattributed event is a weaker record than
        /// an attributed one, but a missing event is not a record at all.
        /// </remarks>
        /// <param name="category">The functional area the event belongs to.</param>
        /// <param name="action">What was done.</param>
        /// <param name="target">The record it was done to.</param>
        /// <param name="deltas">The state changes it produced. May be <see langword="null"/>.</param>
        /// <param name="outcome">Whether it took effect.</param>
        /// <param name="severity">How much attention it warrants.</param>
        /// <returns>The appended event, or <see langword="null"/> when it could not be written.</returns>
        public AuditEvent Record
        (
            AuditCategory category,
            AuditAction action,
            AuditTarget target,
            IEnumerable<AuditDelta> deltas = null,
            AuditOutcome outcome = AuditOutcome.Succeeded,
            AuditSeverity severity = AuditSeverity.Info
        )
        {
            var activity = _ambient.Value;
            var subject = target ?? AuditTarget.None;
            var actorId = ResolveActor(activity);

            var @event = new AuditEvent
            {
                Timestamp = DateTime.UtcNow,
                Origin = activity?.Origin ?? (actorId == Guid.Empty ? AuditOrigin.System : AuditOrigin.User),
                Category = category,
                Action = action,
                Outcome = outcome,
                Severity = severity,
                ActorId = actorId == Guid.Empty ? null : actorId,
                ActorName = ResolveActorName(actorId),
                Agent = activity?.Agent,
                ClientAddress = activity?.ClientAddress,
                TargetType = subject.Type,
                TargetId = subject.Id,
                TargetKey = subject.Key,
                TargetRevision = subject.Revision,
                CorrelationId = activity?.CorrelationId ?? Guid.NewGuid(),
                CausationId = activity?.LastEventId,
                Deltas = [.. (deltas ?? []).Where(x => x is not null)]
            };

            try
            {
                ModelHub.AddAuditEvent(@event);
            }
            catch (Exception ex)
            {
                // the log must not be able to stop the installation; the gap it leaves is
                // detectable through the sequence, which is what makes that safe to accept
                _httpServerContext?.Log?.Exception(ex);

                return null;
            }

            activity?.Observe(@event.Id);

            AuditEventRecorded?.Invoke(this, @event);

            return @event;
        }

        /// <summary>
        /// Appends an event describing what an action did to a record, deriving the deltas from
        /// the record itself.
        /// </summary>
        /// <remarks>
        /// A modification is diffed against the log's own projection of the record rather than
        /// against a pre-image handed in by the caller. That is what lets the whole installation
        /// be audited without every manager having to capture the state of its entity before it
        /// writes - and it means the diff describes what the log learned, which is the thing the
        /// log can actually vouch for.
        /// <para>
        /// The first modification of a record the log has never seen therefore records its
        /// attributes as <see cref="AuditDeltaKind.Added"/> rather than as modifications from an
        /// unknown state. That is not a lie about the record; it is the truth about the log. A
        /// modification "from nothing" would claim knowledge of a previous value that was never
        /// recorded.
        /// </para>
        /// </remarks>
        /// <param name="category">The functional area the event belongs to.</param>
        /// <param name="action">What was done.</param>
        /// <param name="entity">The record it was done to.</param>
        /// <param name="severity">How much attention it warrants.</param>
        /// <returns>The appended event, or <see langword="null"/> when it could not be written.</returns>
        public AuditEvent RecordChange(AuditCategory category, AuditAction action, object entity, AuditSeverity severity = AuditSeverity.Info)
        {
            if (entity is null)
            {
                return null;
            }

            var target = AuditTarget.Describe(entity);

            var deltas = action switch
            {
                AuditAction.Created => AuditSnapshot.Describe(entity, AuditDeltaKind.Added),
                AuditAction.Deleted => AuditSnapshot.Describe(entity, AuditDeltaKind.Removed),
                _ => DiffAgainstLog(target, entity)
            };

            return Record(category, action, target, deltas, AuditOutcome.Succeeded, severity);
        }

        /// <summary>
        /// Returns the events that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching events.</returns>
        public IEnumerable<AuditEvent> GetEvents(IQuery<AuditEvent> query)
        {
            return Hydrate(ModelHub.GetAuditEvents(query));
        }

        /// <summary>
        /// Returns how many events satisfy the supplied query without loading them. No
        /// hydration takes place - a count has no rows to enrich.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted events. Paging must be left off:
        /// a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching events.</returns>
        public int CountEvents(IQuery<AuditEvent> query)
        {
            return ModelHub.CountAuditEvents(query);
        }

        /// <summary>
        /// Returns the events that satisfy the supplied query, executed inside the supplied
        /// query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching events.</returns>
        public IEnumerable<AuditEvent> GetEvents(IQuery<AuditEvent> query, IQueryContext context)
        {
            return Hydrate(ModelHub.GetAuditEvents(query, context as KleeneStarDbContext));
        }

        /// <summary>
        /// Returns a single event by its unique identifier.
        /// </summary>
        /// <param name="eventId">The event id.</param>
        /// <returns>The event, or <see langword="null"/>.</returns>
        public AuditEvent GetEvent(Guid eventId)
        {
            return Hydrate(ModelHub.GetAuditEvent(eventId));
        }

        /// <summary>
        /// Returns a single event by its position in the sequence.
        /// </summary>
        /// <param name="sequence">The 1-based position.</param>
        /// <returns>The event, or <see langword="null"/>.</returns>
        public AuditEvent GetEvent(long sequence)
        {
            return Hydrate(ModelHub.GetAuditEvent(sequence));
        }

        /// <summary>
        /// Returns every event recorded about one record, oldest first.
        /// </summary>
        /// <param name="targetType">The kind of record.</param>
        /// <param name="targetId">The durable id of the record.</param>
        /// <returns>The trail, oldest first.</returns>
        public IEnumerable<AuditEvent> GetTrail(AuditTargetType targetType, Guid targetId)
        {
            return Hydrate(ModelHub.GetAuditTrail(targetType, targetId));
        }

        /// <summary>
        /// Returns every event of one activity, oldest first.
        /// </summary>
        /// <param name="correlationId">The correlation shared by the events.</param>
        /// <returns>The events, oldest first.</returns>
        public IEnumerable<AuditEvent> GetActivity(Guid correlationId)
        {
            return Hydrate(ModelHub.GetAuditActivity(correlationId));
        }

        /// <summary>
        /// Reconstructs the state of one record as the log knows it, at a point in the sequence.
        /// </summary>
        /// <param name="targetType">The kind of record.</param>
        /// <param name="targetId">The durable id of the record.</param>
        /// <param name="atSequence">The position to replay up to, or 0 for the whole trail.</param>
        /// <returns>The projection. Never <see langword="null"/>.</returns>
        public AuditProjection Project(AuditTargetType targetType, Guid targetId, long atSequence = 0)
        {
            var trail = ModelHub.GetAuditTrail(targetType, targetId, atSequence);

            return AuditProjection.Replay(targetType, targetId, trail);
        }

        /// <summary>
        /// Walks the hash chain and reports whether the log is intact.
        /// </summary>
        /// <remarks>
        /// The anchor's own seal cannot be checked, because the event before it may legitimately
        /// be absent - pruned away, or never written because the anchor is the genesis event.
        /// Its recorded <see cref="AuditEvent.PreviousHash"/> is therefore taken as given and
        /// every event after it is verified against the chain that grows from there. Any edit,
        /// deletion, insertion or reordering inside the range breaks it.
        /// </remarks>
        /// <param name="fromSequence">The position to anchor at, or 0 for the oldest event held.</param>
        /// <param name="count">The largest number of events to check, or 0 for all of them.</param>
        /// <returns>The result.</returns>
        public AuditVerification Verify(long fromSequence = 0, int count = 0)
        {
            var checkedCount = 0;
            var missing = new List<long>();

            long? brokenAt = null;
            long from = 0;
            long to = 0;
            long expected = 0;

            string previousHash = null;
            string headHash = null;

            var cursor = fromSequence > 0 ? fromSequence : 0;

            while (brokenAt is null)
            {
                var remaining = count > 0 ? count - checkedCount : VerificationPageSize;

                if (remaining <= 0)
                {
                    break;
                }

                var page = ModelHub.GetAuditRange(cursor, Math.Min(remaining, VerificationPageSize));

                if (page.Count == 0)
                {
                    break;
                }

                foreach (var @event in page)
                {
                    if (checkedCount == 0)
                    {
                        // the anchor: its predecessor is out of range, so its own seal is taken
                        // as the starting point rather than verified against something absent
                        from = @event.Sequence;
                        expected = @event.Sequence;
                    }

                    while (expected < @event.Sequence)
                    {
                        missing.Add(expected);
                        expected++;
                    }

                    if (checkedCount > 0 && !AuditSeal.Verify(@event, previousHash))
                    {
                        brokenAt = @event.Sequence;

                        break;
                    }

                    previousHash = @event.Hash;
                    headHash = @event.Hash;
                    to = @event.Sequence;
                    expected = @event.Sequence + 1;
                    checkedCount++;
                }

                cursor = page[^1].Sequence + 1;
            }

            return new AuditVerification
            {
                FromSequence = from,
                ToSequence = to,
                Checked = checkedCount,
                BrokenAt = brokenAt,
                MissingSequences = missing,
                HeadHash = headHash
            };
        }

        /// <summary>
        /// Removes every event recorded before the supplied moment and records that it did.
        /// </summary>
        /// <remarks>
        /// The removal is followed by an event of its own, naming how many events went, which
        /// range they occupied and the hash the removed range ended on. Without it the log would
        /// simply start later than it used to and nothing would say why - which is
        /// indistinguishable from somebody having deleted the beginning of the trail. With it,
        /// the gap is accounted for by the log itself, and the recorded terminal hash lets an
        /// operator holding an older copy prove the removed range was the one they had.
        /// </remarks>
        /// <param name="before">The retention horizon.</param>
        /// <param name="actorId">The identity ordering the retention run.</param>
        /// <returns>The number of events removed.</returns>
        public int Prune(DateTime before, Guid actorId)
        {
            var (removed, lastSequence, lastHash) = ModelHub.PruneAuditEvents(before);

            if (removed == 0)
            {
                return 0;
            }

            using var activity = BeginActivity(AuditOrigin.System, actorId, "audit.retention");

            Record
            (
                AuditCategory.Lifecycle,
                AuditAction.Pruned,
                AuditTarget.Installation,
                [
                    AuditDelta.Added("removed", removed.ToString(CultureInfo.InvariantCulture), AuditValueKind.Number),
                    AuditDelta.Added("horizon", before.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), AuditValueKind.Timestamp),
                    AuditDelta.Added("lastsequence", lastSequence.ToString(CultureInfo.InvariantCulture), AuditValueKind.Number),
                    AuditDelta.Added("lasthash", lastHash, AuditValueKind.Text)
                ],
                AuditOutcome.Succeeded,
                AuditSeverity.Critical
            );

            return removed;
        }

        /// <summary>
        /// Restores the activity that was ambient before the supplied one was opened.
        /// </summary>
        /// <param name="activity">The activity that is closing.</param>
        internal void Close(AuditActivity activity)
        {
            if (ReferenceEquals(_ambient.Value, activity))
            {
                _ambient.Value = activity.Parent;
            }
        }

        /// <summary>
        /// Produces the deltas between the record as it is now and as the log last knew it.
        /// </summary>
        /// <param name="target">The record the deltas describe.</param>
        /// <param name="entity">The record itself.</param>
        /// <returns>The deltas. Empty when nothing the log records has moved.</returns>
        private static IReadOnlyList<AuditDelta> DiffAgainstLog(AuditTarget target, object entity)
        {
            var current = AuditSnapshot.Capture(entity);

            if (!target.Id.HasValue)
            {
                return AuditSnapshot.Diff(null, current);
            }

            var projection = ModelHub.GetAuditTrail(target.Type, target.Id.Value);

            return AuditSnapshot.Diff
            (
                AuditProjection.Replay(target.Type, target.Id.Value, projection).Values,
                current
            );
        }

        /// <summary>
        /// Returns the identity an event is attributed to: the one the activity names, or the
        /// one the current session resolves to.
        /// </summary>
        /// <param name="activity">The ambient activity, or <see langword="null"/>.</param>
        /// <returns>The identity id, or <see cref="Guid.Empty"/>.</returns>
        private static Guid ResolveActor(AuditActivity activity)
        {
            if (activity is not null && activity.ActorId != Guid.Empty)
            {
                return activity.ActorId;
            }

            if (activity is not null && activity.Origin == AuditOrigin.System)
            {
                return Guid.Empty;
            }

            try
            {
                return CoreHub.SessionManager?.GetCurrentIdentityId(null) ?? Guid.Empty;
            }
            catch (Exception)
            {
                // outside a running host there is no session to resolve; the event is recorded
                // without an actor rather than not recorded
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Returns the display name to snapshot for an actor, so the event stays attributable
        /// after the identity is deleted.
        /// </summary>
        /// <param name="actorId">The identity id, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The name, or <see langword="null"/>.</returns>
        private static string ResolveActorName(Guid actorId)
        {
            if (actorId == Guid.Empty)
            {
                return null;
            }

            try
            {
                var identity = CoreHub.IdentityManager?.GetIdentity(actorId);

                return identity?.Name ?? identity?.UserName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Fills in the navigation the store does not carry: the identity that acted, and the
        /// field definition behind each delta. Either may stay <see langword="null"/> when the
        /// referenced row has since been deleted, which is exactly what the snapshotted names
        /// are there for.
        /// </summary>
        /// <param name="event">The event to hydrate. May be <see langword="null"/>.</param>
        /// <returns>The same event.</returns>
        private static AuditEvent Hydrate(AuditEvent @event)
        {
            if (@event is null)
            {
                return null;
            }

            @event.Actor = @event.ActorId.HasValue
                ? CoreHub.IdentityManager?.GetIdentity(@event.ActorId.Value)
                : null;

            foreach (var delta in @event.Deltas ?? [])
            {
                delta.Field = delta.AttributeId.HasValue
                    ? CoreHub.FieldManager?.GetField(delta.AttributeId.Value)
                    : null;
            }

            return @event;
        }

        /// <summary>
        /// Hydrates a sequence of events, resolving each referenced identity and field once
        /// rather than once per event.
        /// </summary>
        /// <param name="events">The events to hydrate.</param>
        /// <returns>The hydrated events.</returns>
        private static IEnumerable<AuditEvent> Hydrate(IEnumerable<AuditEvent> events)
        {
            var materialized = (events ?? []).ToList();

            var identities = new Dictionary<Guid, Identity>();
            var fields = new Dictionary<Guid, Field>();

            foreach (var @event in materialized)
            {
                if (@event.ActorId.HasValue)
                {
                    if (!identities.TryGetValue(@event.ActorId.Value, out var identity))
                    {
                        identity = CoreHub.IdentityManager?.GetIdentity(@event.ActorId.Value);
                        identities[@event.ActorId.Value] = identity;
                    }

                    @event.Actor = identity;
                }

                foreach (var delta in @event.Deltas ?? [])
                {
                    if (!delta.AttributeId.HasValue)
                    {
                        continue;
                    }

                    if (!fields.TryGetValue(delta.AttributeId.Value, out var field))
                    {
                        field = CoreHub.FieldManager?.GetField(delta.AttributeId.Value);
                        fields[delta.AttributeId.Value] = field;
                    }

                    delta.Field = field;
                }
            }

            return materialized;
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
