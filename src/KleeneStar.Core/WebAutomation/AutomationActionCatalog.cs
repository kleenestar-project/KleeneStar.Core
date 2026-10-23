using KleeneStar.Model.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebAutomation
{
    /// <summary>
    /// An action an automation rule can perform on an object.
    /// </summary>
    /// <remarks>
    /// An action is one line of a rule's script: its key and an argument in the action's own
    /// small syntax (<c>assign agent@example.com</c>, <c>set Impact=High</c>). It validates the
    /// argument when the rule is saved, so a typo is reported to the person writing the rule and
    /// not discovered by a job failing at night. An action must tolerate being run again for the
    /// same job - a job interrupted after the action and before its progress was written resumes
    /// with it - which setting a value is by nature and creating something is through the
    /// creation key the context offers (<see cref="AutomationActionContext.IdempotencyKey"/>).
    /// </remarks>
    public interface IAutomationAction
    {
        /// <summary>
        /// Gets the key a script line starts with.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the i18n key of the line's syntax, shown as help beside the script.
        /// </summary>
        string Syntax { get; }

        /// <summary>
        /// Validates an argument against the class the rule is narrowed to.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <param name="class">The class, or <see langword="null"/> when the rule applies to every
        /// class of its workspace.</param>
        /// <returns><see langword="null"/> when the argument is valid, else what is wrong with it.</returns>
        string Validate(string argument, Class @class);

        /// <summary>
        /// Validates an argument against the workspace and the class of the rule - for an action
        /// whose argument names something a workspace owns, such as a webhook.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <param name="class">The class, or <see langword="null"/>.</param>
        /// <param name="workspaceId">The rule's workspace.</param>
        /// <returns><see langword="null"/> when the argument is valid, else what is wrong with it.</returns>
        string Validate(string argument, Class @class, Guid workspaceId) => Validate(argument, @class);

        /// <summary>
        /// Performs the action.
        /// </summary>
        /// <param name="context">What the action works on.</param>
        /// <param name="argument">The argument.</param>
        void Execute(AutomationActionContext context, string argument);
    }

    /// <summary>
    /// What an action works on.
    /// </summary>
    public sealed class AutomationActionContext
    {
        /// <summary>
        /// Gets the object the action works on, as it stands now.
        /// </summary>
        public ObjectEntity Object { get; init; }

        /// <summary>
        /// Gets the rule the action belongs to.
        /// </summary>
        public AutomationRule Rule { get; init; }

        /// <summary>
        /// Gets the job running the action.
        /// </summary>
        public AutomationJob Job { get; init; }

        /// <summary>
        /// Gets the identity the rule acts as, or <see cref="Guid.Empty"/>.
        /// </summary>
        public Guid ActorId { get; init; }

        /// <summary>
        /// Gets the position of the action in the script.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// Gets a key unique to this action of this job, for an action that creates something
        /// and must not create it twice when the job is resumed.
        /// </summary>
        public string IdempotencyKey => $"automation:{Job?.Id:N}:{Index}";
    }

    /// <summary>
    /// The open catalog of automation actions: the platform registers its own, a plugin
    /// registers more - an integration an outgoing call to its system. There is no enum of actions.
    /// </summary>
    public static class AutomationActionCatalog
    {
        private static readonly ConcurrentDictionary<string, IAutomationAction> _actions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the catalog with the platform's own actions.
        /// </summary>
        static AutomationActionCatalog()
        {
            foreach (var action in Actions.StandardActions.All)
            {
                Register(action);
            }
        }

        /// <summary>
        /// Gets the registered actions, ordered by key.
        /// </summary>
        public static IReadOnlyList<IAutomationAction> All => [.. _actions.Values.OrderBy(x => x.Key, StringComparer.Ordinal)];

        /// <summary>
        /// Registers an action; one registered under an existing key replaces it.
        /// </summary>
        /// <param name="action">The action.</param>
        public static void Register(IAutomationAction action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentException.ThrowIfNullOrWhiteSpace(action.Key);

            _actions[action.Key] = action;
        }

        /// <summary>
        /// Returns an action.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The action, or <see langword="null"/> for an unknown key.</returns>
        public static IAutomationAction Get(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : _actions.GetValueOrDefault(key);
        }
    }
}
