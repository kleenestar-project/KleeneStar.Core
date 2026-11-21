using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The <see cref="IAuditActivity"/> implementation owned by the <see cref="AuditManager"/>.
    /// Holds the context every event recorded inside it inherits, and restores its predecessor
    /// when it closes.
    /// </summary>
    /// <remarks>
    /// Unlike the <see cref="CommitScope"/> it resembles, an activity buffers nothing: an audit
    /// event is written the moment it is recorded, because an event held back until the end of
    /// an action is an event lost if the action crashes - which is precisely the case the log is
    /// most needed for. The activity only supplies context and correlation.
    /// </remarks>
    internal sealed class AuditActivity : IAuditActivity
    {
        private readonly AuditManager _manager;

        private int _depth = 1;
        private bool _closed;

        /// <summary>
        /// Gets the activity that was ambient when this one was opened, restored when it closes.
        /// </summary>
        internal AuditActivity Parent { get; }

        /// <summary>
        /// Gets the correlation shared by every event recorded inside this activity.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Gets what set the activity in motion.
        /// </summary>
        public AuditOrigin Origin { get; private set; }

        /// <summary>
        /// Gets the identity responsible for the activity.
        /// </summary>
        public Guid ActorId { get; private set; }

        /// <summary>
        /// Gets the stable name of the non-human party that acted.
        /// </summary>
        public string Agent { get; private set; }

        /// <summary>
        /// Gets the network address the request arrived from.
        /// </summary>
        public string ClientAddress { get; private set; }

        /// <summary>
        /// Gets the id of the event recorded most recently inside this activity.
        /// </summary>
        public Guid? LastEventId { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="manager">The manager that owns the activity.</param>
        /// <param name="parent">The activity that was ambient when this one was opened.</param>
        /// <param name="origin">What set the activity in motion.</param>
        /// <param name="actorId">The identity responsible, or <see cref="Guid.Empty"/>.</param>
        /// <param name="agent">The non-human party that acted, or <see langword="null"/>.</param>
        /// <param name="clientAddress">The address the request arrived from, or <see langword="null"/>.</param>
        internal AuditActivity(AuditManager manager, AuditActivity parent, AuditOrigin origin, Guid actorId, string agent, string clientAddress)
        {
            _manager = manager;
            Parent = parent;
            CorrelationId = Guid.NewGuid();
            Origin = origin;
            ActorId = actorId;
            Agent = agent;
            ClientAddress = clientAddress;
        }

        /// <summary>
        /// Joins an already open activity, so a manager can open one without knowing whether its
        /// caller already did. The nested <see cref="Dispose"/> then only decrements the depth.
        /// </summary>
        /// <remarks>
        /// The joining caller may know things the outer one did not - a request that resolved
        /// its identity late, a task that learned which integration called it - and those fill
        /// in the blanks. It may not overwrite what the outer activity already established: the
        /// outermost caller is the one that knows what the action as a whole is, and letting an
        /// inner call relabel it would let a nested system task make a user's action look like
        /// the system's.
        /// </remarks>
        /// <param name="actorId">The identity the joining caller names, if any.</param>
        /// <param name="agent">The agent the joining caller names, if any.</param>
        /// <param name="clientAddress">The address the joining caller names, if any.</param>
        internal void Enter(Guid actorId, string agent, string clientAddress)
        {
            _depth++;

            if (ActorId == Guid.Empty && actorId != Guid.Empty)
            {
                ActorId = actorId;
            }

            if (string.IsNullOrWhiteSpace(Agent) && !string.IsNullOrWhiteSpace(agent))
            {
                Agent = agent;
            }

            if (string.IsNullOrWhiteSpace(ClientAddress) && !string.IsNullOrWhiteSpace(clientAddress))
            {
                ClientAddress = clientAddress;
            }
        }

        /// <summary>
        /// Records that an event was written inside this activity, so the next one can name it
        /// as its cause.
        /// </summary>
        /// <param name="eventId">The id of the event just written.</param>
        internal void Observe(Guid eventId)
        {
            LastEventId = eventId;
        }

        /// <summary>
        /// Closes the activity. Only the outermost close restores the ambient context.
        /// </summary>
        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            if (--_depth > 0)
            {
                return;
            }

            _closed = true;

            _manager.Close(this);
        }
    }
}
