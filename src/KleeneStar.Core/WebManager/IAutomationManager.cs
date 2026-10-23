using KleeneStar.Core.WebAutomation;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The automation platform: rules that react to events, conditions and points in time, and
    /// the persistent queue that executes them.
    /// </summary>
    public interface IAutomationManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a rule is added.
        /// </summary>
        event EventHandler<AutomationRule> RuleAdded;

        /// <summary>
        /// An event that fires when a rule is changed.
        /// </summary>
        event EventHandler<AutomationRule> RuleUpdated;

        /// <summary>
        /// An event that fires when a rule is removed.
        /// </summary>
        event EventHandler<AutomationRule> RuleRemoved;

        /// <summary>
        /// An event that fires when a job used its last attempt and waits to be run by hand.
        /// </summary>
        event EventHandler<AutomationJob> JobFailed;

        /// <summary>
        /// Returns the rules of a workspace, or of every workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace, or <see langword="null"/> for all.</param>
        /// <returns>The rules.</returns>
        IReadOnlyList<AutomationRule> GetRules(Guid? workspaceId);

        /// <summary>
        /// Returns a rule.
        /// </summary>
        /// <param name="ruleId">The rule.</param>
        /// <returns>The rule, or <see langword="null"/>.</returns>
        AutomationRule GetRule(Guid ruleId);

        /// <summary>
        /// Validates a rule: its trigger, its condition and every line of its script.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The problems; empty for a valid rule.</returns>
        IReadOnlyList<string> Validate(AutomationRule rule);

        /// <summary>
        /// Adds a rule.
        /// </summary>
        /// <param name="rule">The rule, valid.</param>
        /// <returns>The manager.</returns>
        IAutomationManager AddRule(AutomationRule rule);

        /// <summary>
        /// Writes the changes of a rule.
        /// </summary>
        /// <param name="rule">The rule, valid.</param>
        /// <returns>The manager.</returns>
        IAutomationManager UpdateRule(AutomationRule rule);

        /// <summary>
        /// Removes a rule and its jobs.
        /// </summary>
        /// <param name="ruleId">The rule.</param>
        /// <returns>The manager.</returns>
        IAutomationManager RemoveRule(Guid ruleId);

        /// <summary>
        /// Queues a job for every active rule the event satisfies.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <returns>How many jobs were queued; an occurrence delivered before queues none.</returns>
        int Raise(AutomationEvent @event);

        /// <summary>
        /// Runs the jobs that are due.
        /// </summary>
        /// <param name="limit">How many jobs to run at most.</param>
        /// <returns>How many jobs were run.</returns>
        int ProcessDue(int limit = 20);

        /// <summary>
        /// Evaluates the scheduled rules whose time has come.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>How many jobs were queued.</returns>
        int RunSchedules(DateTime now);

        /// <summary>
        /// Puts a failed job back into the queue, with its attempts reset.
        /// </summary>
        /// <param name="jobId">The job.</param>
        /// <returns>The job, or <see langword="null"/> when it is unknown or not failed.</returns>
        AutomationJob Retry(Guid jobId);

        /// <summary>
        /// Returns the jobs of the rules of a workspace, newest first.
        /// </summary>
        /// <param name="workspaceId">The workspace, or <see langword="null"/> for all.</param>
        /// <param name="state">The state, or <see langword="null"/> for any.</param>
        /// <param name="limit">How many jobs to return at most.</param>
        /// <returns>The jobs.</returns>
        IReadOnlyList<AutomationJob> GetJobs(Guid? workspaceId, AutomationJobState? state, int limit);

        /// <summary>
        /// Subscribes to the events the triggers are raised from. Called once at start-up;
        /// calling it again does nothing.
        /// </summary>
        void Connect();
    }
}
