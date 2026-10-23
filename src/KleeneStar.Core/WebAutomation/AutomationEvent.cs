using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebAutomation
{
    /// <summary>
    /// Something that happened to an object and that automation rules may react to.
    /// </summary>
    /// <remarks>
    /// An event is raised by the platform itself (a commit, a transition, a comment, the
    /// scheduler) or by an integration (an incoming webhook). It names its trigger, the object it
    /// concerns and a key that identifies <em>this</em> occurrence: the same occurrence delivered
    /// twice carries the same key, which is what keeps the queue from running a rule twice for it.
    /// </remarks>
    public sealed class AutomationEvent
    {
        /// <summary>
        /// Gets the key of the trigger in the trigger catalog.
        /// </summary>
        public string Trigger { get; init; }

        /// <summary>
        /// Gets the object the event concerns.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets what the event is about, matched against a rule's trigger parameter - the target
        /// state of a transition, the attributes an update changed, the audience of a comment.
        /// </summary>
        public IReadOnlyCollection<string> Subjects { get; init; } = [];

        /// <summary>
        /// Gets the key identifying the occurrence; two deliveries of it carry the same key.
        /// </summary>
        public string Occurrence { get; init; }

        /// <summary>
        /// Gets the identity that caused the event, or <see cref="Guid.Empty"/> for the system.
        /// </summary>
        public Guid ActorId { get; init; }
    }
}
