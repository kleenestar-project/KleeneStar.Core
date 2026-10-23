using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebAutomation
{
    /// <summary>
    /// A trigger an automation rule can react to.
    /// </summary>
    public interface IAutomationTrigger
    {
        /// <summary>
        /// Gets the key the rule stores, e.g. <c>transition.executed</c>.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the i18n key of the trigger's label.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the i18n key describing what the trigger parameter means, or <see langword="null"/>
        /// for a trigger that takes none.
        /// </summary>
        string Parameter { get; }

        /// <summary>
        /// Gets a value indicating whether the trigger is evaluated by the scheduler rather than
        /// raised by an event.
        /// </summary>
        bool Scheduled => false;
    }

    /// <summary>
    /// The open catalog of automation triggers: the platform registers its own, a plugin
    /// registers more - an integration its incoming events - the way object kinds and renderers
    /// are registered. There is no enum of triggers.
    /// </summary>
    public static class AutomationTriggerCatalog
    {
        /// <summary>An object was created.</summary>
        public const string ObjectCreated = "object.created";

        /// <summary>An object was changed; the parameter narrows to the attributes that changed.</summary>
        public const string ObjectUpdated = "object.updated";

        /// <summary>A workflow transition was executed; the parameter narrows to the target state.</summary>
        public const string TransitionExecuted = "transition.executed";

        /// <summary>A comment was added; the parameter narrows to its audience.</summary>
        public const string CommentAdded = "comment.added";

        /// <summary>A service level target was missed; the parameter narrows to the target.</summary>
        public const string SlaBreached = "sla.breached";

        /// <summary>The scheduler runs the rule every so many minutes over the objects of its scope.</summary>
        public const string Schedule = "schedule";

        /// <summary>
        /// The key of the trigger another system raises through an incoming webhook.
        /// </summary>
        public const string IntegrationReceived = "integration.received";

        /// <summary>The key of the trigger the scheduler raises when a service level target passes its warning threshold.</summary>
        public const string SlaAtRisk = "sla.atrisk";

        /// <summary>The key of the trigger raised when an approval is requested.</summary>
        public const string ApprovalRequested = "approval.requested";

        /// <summary>The key of the trigger raised when an approval ends.</summary>
        public const string ApprovalCompleted = "approval.completed";

        /// <summary>The key of the trigger raised when a step of an approval took longer than it may.</summary>
        public const string ApprovalOverdue = "approval.overdue";

        private static readonly ConcurrentDictionary<string, IAutomationTrigger> _triggers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the catalog with the platform's own triggers.
        /// </summary>
        static AutomationTriggerCatalog()
        {
            Register(new Trigger(ObjectCreated, "kleenestar.core:automation.trigger.object.created", null));
            Register(new Trigger(ObjectUpdated, "kleenestar.core:automation.trigger.object.updated", "kleenestar.core:automation.trigger.object.updated.parameter"));
            Register(new Trigger(TransitionExecuted, "kleenestar.core:automation.trigger.transition.executed", "kleenestar.core:automation.trigger.transition.executed.parameter"));
            Register(new Trigger(CommentAdded, "kleenestar.core:automation.trigger.comment.added", "kleenestar.core:automation.trigger.comment.added.parameter"));
            Register(new Trigger(SlaAtRisk, "kleenestar.core:automation.trigger.sla.atrisk", "kleenestar.core:automation.trigger.sla.atrisk.parameter", scheduled: true));
            Register(new Trigger(SlaBreached, "kleenestar.core:automation.trigger.sla.breached", "kleenestar.core:automation.trigger.sla.breached.parameter", scheduled: true));
            Register(new Trigger(ApprovalRequested, "kleenestar.core:automation.trigger.approval.requested", "kleenestar.core:automation.trigger.approval.requested.parameter"));
            Register(new Trigger(ApprovalCompleted, "kleenestar.core:automation.trigger.approval.completed", "kleenestar.core:automation.trigger.approval.completed.parameter"));
            Register(new Trigger(ApprovalOverdue, "kleenestar.core:automation.trigger.approval.overdue", "kleenestar.core:automation.trigger.approval.overdue.parameter"));
            Register(new Trigger(IntegrationReceived, "kleenestar.core:automation.trigger.integration.received", "kleenestar.core:automation.trigger.integration.received.parameter"));
            Register(new Trigger(Schedule, "kleenestar.core:automation.trigger.schedule", "kleenestar.core:automation.trigger.schedule.parameter", scheduled: true));
        }

        /// <summary>
        /// Gets the registered triggers, ordered by key.
        /// </summary>
        public static IReadOnlyList<IAutomationTrigger> Triggers => [.. _triggers.Values.OrderBy(x => x.Key, StringComparer.Ordinal)];

        /// <summary>
        /// Registers a trigger; a trigger registered under an existing key replaces it.
        /// </summary>
        /// <param name="trigger">The trigger.</param>
        public static void Register(IAutomationTrigger trigger)
        {
            ArgumentNullException.ThrowIfNull(trigger);
            ArgumentException.ThrowIfNullOrWhiteSpace(trigger.Key);

            _triggers[trigger.Key] = trigger;
        }

        /// <summary>
        /// Returns a trigger.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The trigger, or <see langword="null"/> for an unknown key.</returns>
        public static IAutomationTrigger Get(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : _triggers.GetValueOrDefault(key);
        }

        /// <summary>
        /// Determines whether an event satisfies a rule's trigger parameter: a rule naming no
        /// parameter reacts to every occurrence, one naming a parameter to the occurrences about
        /// it (a comma-separated list names several).
        /// </summary>
        /// <param name="parameter">The rule's trigger parameter.</param>
        /// <param name="subjects">What the event is about.</param>
        /// <returns><see langword="true"/> when the rule reacts.</returns>
        public static bool Matches(string parameter, IReadOnlyCollection<string> subjects)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                return true;
            }

            var wanted = parameter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return (subjects ?? []).Any(x => wanted.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// A trigger of the platform.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="label">The label.</param>
        /// <param name="parameter">The parameter description, or <see langword="null"/>.</param>
        /// <param name="scheduled">Whether the scheduler evaluates the trigger.</param>
        private sealed class Trigger(string key, string label, string parameter, bool scheduled = false) : IAutomationTrigger
        {
            /// <inheritdoc/>
            public string Key => key;

            /// <inheritdoc/>
            public string Label => label;

            /// <inheritdoc/>
            public string Parameter => parameter;

            /// <inheritdoc/>
            public bool Scheduled => scheduled;
        }
    }
}
