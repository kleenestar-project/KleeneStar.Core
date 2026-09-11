using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebWorkflow
{
    /// <summary>
    /// Everything a guard, a validator or a post function is told about the state change it is
    /// asked about.
    /// </summary>
    /// <remarks>
    /// One context for all three stages, because they ask the same questions of the same move and
    /// a rule that changes stage should not have to be rewritten: a condition that guards a
    /// transition today is often the one that validates it tomorrow. What differs is when it is
    /// asked and what its answer means, and that is the business of the stage rather than of the
    /// rule.
    /// <para>
    /// The context is read-only for guards and validators - they answer a question and change
    /// nothing - while a post function is expected to act on the world through the managers. That
    /// is not enforced by the type, because it cannot be: a rule contributed by a plugin can
    /// reach the managers either way. It is stated here so a reviewer knows which of the three a
    /// write belongs in.
    /// </para>
    /// </remarks>
    public sealed class WorkflowRuleContext
    {
        /// <summary>
        /// Gets the object being moved.
        /// </summary>
        public ObjectEntity Object { get; init; }

        /// <summary>
        /// Gets the workflow-backed field carrying the state.
        /// </summary>
        public Field Field { get; init; }

        /// <summary>
        /// Gets the state the object is leaving, or <see langword="null"/> when it is entering
        /// the state machine for the first time.
        /// </summary>
        public Status Source { get; init; }

        /// <summary>
        /// Gets the state the object is moving to.
        /// </summary>
        public Status Target { get; init; }

        /// <summary>
        /// Gets the transition being taken, or <see langword="null"/> for an entry move, which
        /// travels along no transition and can therefore carry no rules of its own.
        /// </summary>
        public Transition Transition { get; init; }

        /// <summary>
        /// Gets the identity making the move, or <see cref="Guid.Empty"/> when the move is made
        /// by the system - a cascade closing a duplicate, a scheduled escalation.
        /// </summary>
        public Guid IdentityId { get; init; }

        /// <summary>
        /// Gets what was filled in on the screen of the transition, keyed by field name, or an
        /// empty map when the transition shows no screen.
        /// </summary>
        /// <remarks>
        /// The values are what the person typed, not what is stored: they are available to the
        /// validators <em>before</em> anything is written, which is the point of a screen - a
        /// transition can demand a resolution note and refuse the move without it, rather than
        /// accepting the move and then finding the note missing.
        /// </remarks>
        public IReadOnlyDictionary<string, string> ScreenValues { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns what the screen carries under the supplied field name, or
        /// <see langword="null"/> when it carries nothing.
        /// </summary>
        /// <param name="name">The field name, compared case-insensitively.</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        public string Screen(string name)
        {
            return name is not null && ScreenValues.TryGetValue(name, out var value) ? value : null;
        }
    }
}
