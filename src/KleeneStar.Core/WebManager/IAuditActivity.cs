using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for the ambient context one action is audited under: who is acting,
    /// what kind of trigger they are, and the correlation the events of that action share.
    /// </summary>
    /// <remarks>
    /// Without an activity, every audit event would have to be handed the actor and the origin
    /// by whoever records it, and a manager three calls down the stack has neither. The activity
    /// is opened once where the request is understood - the page, the REST endpoint, the
    /// scheduled task - and every event recorded inside it inherits that context, however deep
    /// the call went.
    /// </remarks>
    public interface IAuditActivity : IDisposable
    {
        /// <summary>
        /// Gets the correlation shared by every event recorded inside this activity.
        /// </summary>
        Guid CorrelationId { get; }

        /// <summary>
        /// Gets what set the activity in motion.
        /// </summary>
        AuditOrigin Origin { get; }

        /// <summary>
        /// Gets the identity responsible for the activity, or <see cref="Guid.Empty"/> when
        /// none was resolved.
        /// </summary>
        Guid ActorId { get; }

        /// <summary>
        /// Gets the stable name of the non-human party that acted, or <see langword="null"/>
        /// for a person acting through the interface.
        /// </summary>
        string Agent { get; }

        /// <summary>
        /// Gets the network address the request arrived from, or <see langword="null"/>.
        /// </summary>
        string ClientAddress { get; }

        /// <summary>
        /// Gets the id of the event recorded most recently inside this activity, which the next
        /// one is attributed to as its cause. <see langword="null"/> until the first event.
        /// </summary>
        Guid? LastEventId { get; }
    }
}
