using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The automation platform: captures events, queues a job per matching rule, and runs the
    /// queue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Capture.</b> The triggers are raised from what the platform already announces - a
    /// commit (<c>object.created</c>, <c>object.updated</c> with the attributes it changed), a
    /// transition, a comment - and by the scheduler (<c>schedule</c>, <c>sla.breached</c>). An
    /// integration raises its own through <see cref="Raise"/>. The condition is evaluated when
    /// the event happens, against the object as the event left it; a rule reacts to what
    /// happened, not to what the object looks like by the time a worker gets to it.
    /// </para>
    /// <para>
    /// <b>Queue.</b> Every match is written as an <see cref="AutomationJob"/> first, keyed by
    /// rule and occurrence, so nothing is lost to a crash and nothing runs twice for a replayed
    /// event. Workers claim jobs under a lease and optimistic concurrency, which makes the queue
    /// safe for several replicas sharing a database.
    /// </para>
    /// <para>
    /// <b>Run.</b> A job runs its rule's script line by line as the system - unrestricted, the
    /// writes attributed to the rule's identity, recorded with the automation origin - and
    /// records each finished line, so a resumed job continues after the last one. A failure is
    /// retried with a growing delay (30 s, 2 min, 8 min, 32 min); after <see cref="MaxAttempts"/>
    /// the job waits as <see cref="AutomationJobState.Failed"/> with its error until somebody
    /// retries it by hand.
    /// </para>
    /// <para>
    /// <b>Loops.</b> What a rule does raises events of its own, and a rule reacting to them could
    /// run forever. An event raised while a job runs is attributed to it: it does not trigger the
    /// same rule again, and the chain stops after <see cref="MaxDepth"/> links.
    /// </para>
    /// </remarks>
    public sealed class AutomationManager : IAutomationManager
    {
        /// <summary>
        /// How many attempts a job gets before it waits to be run by hand.
        /// </summary>
        public const int MaxAttempts = 5;

        /// <summary>
        /// How many rules may react to each other's consequences in a row.
        /// </summary>
        public const int MaxDepth = 3;

        /// <summary>
        /// How long a worker holds a job it claimed.
        /// </summary>
        public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How often a rule on the <c>sla.breached</c> trigger looks at its objects.
        /// </summary>
        public static readonly TimeSpan SlaInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The job running on this call chain, to which the events its actions raise belong.
        /// </summary>
        private static readonly AsyncLocal<AutomationJob> _running = new();

        /// <summary>
        /// The compiled conditions of the rules, by rule, version and text.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Guid RuleId, DateTime Updated, string Condition), Func<ObjectEntity, bool>> _conditions = new();

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly string _worker = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        private readonly object _gate = new();
        private bool _connected;

        /// <inheritdoc/>
        public event EventHandler<AutomationRule> RuleAdded;

        /// <inheritdoc/>
        public event EventHandler<AutomationRule> RuleUpdated;

        /// <inheritdoc/>
        public event EventHandler<AutomationRule> RuleRemoved;

        /// <inheritdoc/>
        public event EventHandler<AutomationJob> JobFailed;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AutomationManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public IReadOnlyList<AutomationRule> GetRules(Guid? workspaceId)
        {
            return ModelHub.GetAutomationRules(workspaceId);
        }

        /// <inheritdoc/>
        public AutomationRule GetRule(Guid ruleId)
        {
            return ModelHub.GetAutomationRule(ruleId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Validate(AutomationRule rule)
        {
            var problems = new List<string>();

            if (rule is null)
            {
                problems.Add("The rule is missing.");
                return problems;
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                problems.Add("The rule needs a name.");
            }

            var trigger = AutomationTriggerCatalog.Get(rule.Trigger);

            if (trigger is null)
            {
                problems.Add($"Unknown trigger '{rule.Trigger}'.");
            }
            else if (string.Equals(trigger.Key, AutomationTriggerCatalog.Schedule, StringComparison.OrdinalIgnoreCase)
                && (!int.TryParse(rule.TriggerParameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) || minutes < 1))
            {
                problems.Add("A schedule needs its interval in minutes.");
            }

            if (!string.IsNullOrWhiteSpace(rule.Condition) && !WqlFilter.TryValidate<ObjectEntity>(rule.Condition, out var error))
            {
                problems.Add($"The condition is not valid WQL: {error}");
            }

            var @class = rule.ClassId is Guid classId ? CoreHub.ClassManager.GetClass(classId) : null;

            if (rule.ClassId is not null && (@class is null || @class.WorkspaceId != rule.WorkspaceId))
            {
                problems.Add("The class does not belong to the rule's workspace.");
            }

            problems.AddRange(AutomationScript.Validate(rule.Actions, @class, rule.WorkspaceId));

            return problems;
        }

        /// <inheritdoc/>
        public IAutomationManager AddRule(AutomationRule rule)
        {
            Require(rule);

            rule.Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id;
            rule.Created = rule.Created == default ? DateTime.UtcNow : rule.Created;
            rule.Updated = DateTime.UtcNow;
            rule.NextRun = FirstRun(rule, DateTime.UtcNow);

            ModelHub.AddAutomationRule(rule);
            RuleAdded?.Invoke(this, rule);

            return this;
        }

        /// <inheritdoc/>
        public IAutomationManager UpdateRule(AutomationRule rule)
        {
            Require(rule);

            rule.Updated = DateTime.UtcNow;
            rule.NextRun = FirstRun(rule, DateTime.UtcNow);

            ModelHub.UpdateAutomationRule(rule);
            RuleUpdated?.Invoke(this, rule);

            return this;
        }

        /// <inheritdoc/>
        public IAutomationManager RemoveRule(Guid ruleId)
        {
            var rule = ModelHub.GetAutomationRule(ruleId);

            if (rule is null)
            {
                return this;
            }

            ModelHub.RemoveAutomationRule(ruleId);
            RuleRemoved?.Invoke(this, rule);

            return this;
        }

        /// <inheritdoc/>
        public int Raise(AutomationEvent @event)
        {
            if (@event is null || string.IsNullOrWhiteSpace(@event.Trigger) || string.IsNullOrWhiteSpace(@event.Occurrence))
            {
                return 0;
            }

            var cause = _running.Value;
            var depth = cause is null ? 0 : cause.Depth + 1;

            // a chain of rules reacting to each other's consequences ends here rather than never
            if (depth > MaxDepth)
            {
                return 0;
            }

            ObjectEntity @object;

            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                @object = CoreHub.ObjectManager.GetObject(@event.ObjectId);
            }

            if (@object is null)
            {
                return 0;
            }

            var queued = 0;

            foreach (var rule in ModelHub.GetAutomationRules(@object.WorkspaceId))
            {
                if (rule.State != AutomationRuleState.Active
                    || !string.Equals(rule.Trigger, @event.Trigger, StringComparison.OrdinalIgnoreCase)
                    || (rule.ClassId is Guid classId && classId != @object.ClassId)
                    || rule.Id == cause?.RuleId
                    || (!IsInterval(rule) && !AutomationTriggerCatalog.Matches(rule.TriggerParameter, @event.Subjects))
                    || !Holds(rule, @object))
                {
                    continue;
                }

                queued += Enqueue(rule, @object.Id, @event.Trigger, @event.Occurrence, depth);
            }

            return queued;
        }

        /// <summary>
        /// Queues the job of one rule for one object, unless the occurrence was queued before.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="objectId">The object.</param>
        /// <param name="trigger">The trigger.</param>
        /// <param name="occurrence">The occurrence.</param>
        /// <param name="depth">The depth of the chain the job belongs to.</param>
        /// <returns>1 when queued, 0 when it was queued before.</returns>
        private static int Enqueue(AutomationRule rule, Guid objectId, string trigger, string occurrence, int depth)
        {
            var job = new AutomationJob
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                ObjectId = objectId,
                Trigger = trigger,
                Deduplication = Truncate($"{rule.Id:N}:{occurrence}", 256),
                Depth = depth,
                State = AutomationJobState.Pending,
                NextAttempt = DateTime.UtcNow,
                Version = Guid.NewGuid(),
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            return ModelHub.TryEnqueueAutomationJob(job) ? 1 : 0;
        }

        /// <inheritdoc/>
        public int ProcessDue(int limit = 20)
        {
            var jobs = ModelHub.ClaimAutomationJobs(_worker, DateTime.UtcNow, Lease, limit);

            foreach (var job in jobs)
            {
                Run(job);
            }

            return jobs.Count;
        }

        /// <inheritdoc/>
        public int RunSchedules(DateTime now)
        {
            var queued = 0;

            foreach (var rule in ModelHub.GetAutomationRules())
            {
                var trigger = AutomationTriggerCatalog.Get(rule.Trigger);

                if (rule.State != AutomationRuleState.Active || trigger?.Scheduled != true || rule.NextRun > now)
                {
                    continue;
                }

                try
                {
                    queued += string.Equals(trigger.Key, AutomationTriggerCatalog.SlaBreached, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(trigger.Key, AutomationTriggerCatalog.SlaAtRisk, StringComparison.OrdinalIgnoreCase)
                        ? ScheduleSla(rule, now)
                        : ScheduleInterval(rule, now);
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }

                rule.NextRun = now + Interval(rule);
                ModelHub.UpdateAutomationRule(rule);
            }

            return queued;
        }

        /// <inheritdoc/>
        public AutomationJob Retry(Guid jobId)
        {
            var job = ModelHub.GetAutomationJob(jobId);

            if (job is null || job.State != AutomationJobState.Failed)
            {
                return null;
            }

            // the actions already done stay done; the job continues after the last one
            job.State = AutomationJobState.Pending;
            job.Attempts = 0;
            job.NextAttempt = DateTime.UtcNow;
            job.LockedBy = null;
            job.LockedUntil = null;
            ModelHub.UpdateAutomationJob(job);

            return job;
        }

        /// <inheritdoc/>
        public IReadOnlyList<AutomationJob> GetJobs(Guid? workspaceId, AutomationJobState? state, int limit)
        {
            var rules = workspaceId is null
                ? null
                : ModelHub.GetAutomationRules(workspaceId).Select(x => x.Id).ToList();

            return ModelHub.GetAutomationJobs(rules, state, limit);
        }

        /// <inheritdoc/>
        public void Connect()
        {
            lock (_gate)
            {
                if (_connected)
                {
                    return;
                }

                _connected = true;
            }

            CoreHub.CommitManager.CommitAdded += (_, commit) => Capture(() => OnCommit(commit));
            CoreHub.WorkflowManager.TransitionExecuted += (_, result) => Capture(() => OnTransition(result));
            CoreHub.CommentManager.CommentAdded += (_, comment) => Capture(() => OnComment(comment));
        }

        /// <summary>
        /// Runs a capture without letting a failure reach the write that raised the event: the
        /// write was legitimate, and a rule not being told is the smaller fault.
        /// </summary>
        /// <param name="capture">The capture.</param>
        private void Capture(Action capture)
        {
            try
            {
                capture();
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Raises <c>object.created</c> or <c>object.updated</c> from a commit.
        /// </summary>
        /// <param name="commit">The commit.</param>
        private void OnCommit(Commit commit)
        {
            if (commit is null)
            {
                return;
            }

            var trigger = commit.Type == CommitType.Created ? AutomationTriggerCatalog.ObjectCreated : AutomationTriggerCatalog.ObjectUpdated;
            var subjects = (commit.Changes ?? [])
                .Select(x => x.Field?.Name ?? (x.FieldId is Guid fieldId ? CoreHub.FieldManager.GetField(fieldId)?.Name : null) ?? x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Raise(new AutomationEvent
            {
                Trigger = trigger,
                ObjectId = commit.ObjectId,
                Subjects = subjects,
                Occurrence = $"commit:{commit.Id:N}",
                ActorId = commit.CreatedById ?? Guid.Empty
            });
        }

        /// <summary>
        /// Raises <c>transition.executed</c> from a completed move.
        /// </summary>
        /// <param name="result">The move.</param>
        private void OnTransition(WorkflowTransitionResult result)
        {
            if (result?.Outcome != WorkflowTransitionOutcome.Executed || result.Target is null)
            {
                return;
            }

            Raise(new AutomationEvent
            {
                Trigger = AutomationTriggerCatalog.TransitionExecuted,
                ObjectId = result.ObjectId,
                Subjects = [result.Target.Name],
                Occurrence = $"transition:{result.ObjectId:N}:{result.Target.Id:N}:{DateTime.UtcNow.Ticks}"
            });
        }

        /// <summary>
        /// Raises <c>comment.added</c> from a new comment.
        /// </summary>
        /// <param name="comment">The comment.</param>
        private void OnComment(Comment comment)
        {
            if (comment is null)
            {
                return;
            }

            Raise(new AutomationEvent
            {
                Trigger = AutomationTriggerCatalog.CommentAdded,
                ObjectId = comment.ObjectId,
                Subjects = [comment.Visibility == CommentVisibility.InternalTeam ? "internal" : "public"],
                Occurrence = $"comment:{comment.Id:N}",
                ActorId = comment.AuthorId
            });
        }

        /// <summary>
        /// Runs one claimed job.
        /// </summary>
        /// <param name="job">The job.</param>
        private void Run(AutomationJob job)
        {
            var rule = ModelHub.GetAutomationRule(job.RuleId);
            ObjectEntity @object;

            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                @object = CoreHub.ObjectManager.GetObject(job.ObjectId);
            }

            if (rule is null || rule.State != AutomationRuleState.Active || @object is null)
            {
                Finish(job, AutomationJobState.Skipped, null);
                return;
            }

            var actor = rule.RunAsId ?? rule.CreatedById ?? Guid.Empty;
            var previous = _running.Value;

            _running.Value = job;

            try
            {
                using var identity = actor == Guid.Empty ? null : CoreHub.SessionManager.BeginIdentity(actor);
                using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();
                using var silent = CoreHub.BeginSilentNotifications();
                using var activity = CoreHub.AuditManager?.BeginActivity(AuditOrigin.Automation, actor, $"automation:{rule.Id:N}");

                var lines = AutomationScript.Parse(rule.Actions);

                for (var index = job.CompletedActions; index < lines.Count; index++)
                {
                    var line = lines[index];
                    var action = AutomationActionCatalog.Get(line.Key)
                        ?? throw new InvalidOperationException($"Line {line.Number}: unknown action '{line.Key}'.");

                    // every action works on the object as the previous one left it
                    using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
                    {
                        @object = CoreHub.ObjectManager.GetObject(job.ObjectId) ?? @object;
                    }

                    action.Execute(new AutomationActionContext
                    {
                        Object = @object,
                        Rule = rule,
                        Job = job,
                        ActorId = actor,
                        Index = index
                    }, line.Argument);

                    job.CompletedActions = index + 1;
                    ModelHub.UpdateAutomationJob(job);
                }

                Finish(job, AutomationJobState.Succeeded, null);
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
            finally
            {
                _running.Value = previous;
            }
        }

        /// <summary>
        /// Ends a job.
        /// </summary>
        /// <param name="job">The job.</param>
        /// <param name="state">Its final state.</param>
        /// <param name="error">The error to keep, or <see langword="null"/>.</param>
        private static void Finish(AutomationJob job, AutomationJobState state, string error)
        {
            job.State = state;
            job.LastError = error;
            job.LockedBy = null;
            job.LockedUntil = null;
            ModelHub.UpdateAutomationJob(job);
        }

        /// <summary>
        /// Records a failed attempt: retried later with a growing delay, or parked after the last.
        /// </summary>
        /// <param name="job">The job.</param>
        /// <param name="error">The failure.</param>
        private void Fail(AutomationJob job, Exception error)
        {
            var message = (error is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : error)?.Message;

            if (job.Attempts >= MaxAttempts)
            {
                Finish(job, AutomationJobState.Failed, message);
                JobFailed?.Invoke(this, job);

                return;
            }

            job.State = AutomationJobState.Pending;
            job.LastError = message;
            job.LockedBy = null;
            job.LockedUntil = null;
            job.NextAttempt = DateTime.UtcNow + Backoff(job.Attempts);
            ModelHub.UpdateAutomationJob(job);
        }

        /// <summary>
        /// Returns the delay before the next attempt: 30 seconds, quadrupled per attempt.
        /// </summary>
        /// <param name="attempts">The attempts made.</param>
        /// <returns>The delay.</returns>
        public static TimeSpan Backoff(int attempts)
        {
            return TimeSpan.FromSeconds(30 * Math.Pow(4, Math.Max(0, attempts - 1)));
        }

        /// <summary>
        /// Queues a job for every object of a scheduled rule's scope its condition holds for,
        /// once per interval.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="now">The current time.</param>
        /// <returns>How many jobs were queued.</returns>
        private int ScheduleInterval(AutomationRule rule, DateTime now)
        {
            var interval = Interval(rule);
            var bucket = now.Ticks / interval.Ticks;
            var queued = 0;

            // the rule's own jobs, not an event every rule of the workspace hears: another
            // scheduled rule runs on its own interval
            foreach (var @object in Matching(rule))
            {
                queued += Enqueue(rule, @object.Id, rule.Trigger, $"schedule:{@object.Id:N}:{bucket}", 0);
            }

            return queued;
        }

        /// <summary>
        /// Queues a job for every object of the rule's scope with a missed service level target -
        /// once per target and cycle, however long it stays missed.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="now">The current time.</param>
        /// <returns>How many jobs were queued.</returns>
        private int ScheduleSla(AutomationRule rule, DateTime now)
        {
            // a breach is announced once per target and cycle, and so is the warning before it
            var atRisk = string.Equals(rule.Trigger, AutomationTriggerCatalog.SlaAtRisk, StringComparison.OrdinalIgnoreCase);
            var wanted = atRisk ? TypeStatusSla.AtRisk : TypeStatusSla.Violated;
            var queued = 0;

            foreach (var @object in Matching(rule))
            {
                foreach (var (policy, target, evaluation) in SlaEvaluations.For(@object, now))
                {
                    if (evaluation.Status != wanted || !AutomationTriggerCatalog.Matches(rule.TriggerParameter, [target.Name, policy.Name]))
                    {
                        continue;
                    }

                    queued += Enqueue(rule, @object.Id, rule.Trigger, $"{(atRisk ? "slarisk" : "sla")}:{@object.Id:N}:{target.Id:N}:{evaluation.Cycle}", 0);
                }
            }

            return queued;
        }

        /// <summary>
        /// Returns the active objects a rule applies to.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The objects.</returns>
        private static List<ObjectEntity> Scope(AutomationRule rule)
        {
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            var query = new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, rule.WorkspaceId);

            if (rule.ClassId is Guid classId)
            {
                query = query.WhereEquals(x => x.ClassId, classId);
            }

            return [.. CoreHub.ObjectManager.GetObjects(query).Where(x => x.State == WorkspaceState.Active)];
        }

        /// <summary>
        /// Determines whether a rule's condition holds for an object.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="object">The object.</param>
        /// <returns><see langword="true"/> when the rule has no condition or it holds.</returns>
        private static bool Holds(AutomationRule rule, ObjectEntity @object)
        {
            return Condition(rule)(@object);
        }

        /// <summary>
        /// Returns the objects of a rule's scope its condition holds for.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The objects.</returns>
        private static IEnumerable<ObjectEntity> Matching(AutomationRule rule)
        {
            var condition = Condition(rule);

            return Scope(rule).Where(condition);
        }

        /// <summary>
        /// Returns a rule's condition as a compiled predicate, compiled once per version of the
        /// rule - a scheduler testing it against every object of a workspace would otherwise
        /// compile it once per object.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The predicate.</returns>
        private static Func<ObjectEntity, bool> Condition(AutomationRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Condition))
            {
                return _ => true;
            }

            return _conditions.GetOrAdd((rule.Id, rule.Updated, rule.Condition), key =>
            {
                try
                {
                    var compiled = WqlFilter.Compile<ObjectEntity>(key.Condition)?.Compile();

                    return compiled ?? (_ => true);
                }
                catch (Exception)
                {
                    // a condition that stopped compiling - a field renamed under it - matches
                    // nothing rather than everything
                    return _ => false;
                }
            });
        }

        /// <summary>
        /// Determines whether a rule runs on an interval, whose parameter is its period in
        /// minutes rather than a filter on what an event is about.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns><see langword="true"/> for a rule on the schedule trigger.</returns>
        private static bool IsInterval(AutomationRule rule)
        {
            return string.Equals(rule.Trigger, AutomationTriggerCatalog.Schedule, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns how often a scheduled rule is evaluated.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The interval.</returns>
        private static TimeSpan Interval(AutomationRule rule)
        {
            return string.Equals(rule.Trigger, AutomationTriggerCatalog.Schedule, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rule.TriggerParameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
                ? TimeSpan.FromMinutes(minutes)
                : SlaInterval;
        }

        /// <summary>
        /// Returns when a scheduled rule is first evaluated: right away. A rule on an event has
        /// no schedule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The time, or <see langword="null"/>.</returns>
        private static DateTime? FirstRun(AutomationRule rule, DateTime now)
        {
            return AutomationTriggerCatalog.Get(rule.Trigger)?.Scheduled == true ? now : null;
        }

        /// <summary>
        /// Refuses an invalid rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <exception cref="ArgumentException">The rule is invalid; the message lists the problems.</exception>
        private void Require(AutomationRule rule)
        {
            var problems = Validate(rule);

            if (problems.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", problems), nameof(rule));
            }
        }

        /// <summary>
        /// Shortens a key to a column's length.
        /// </summary>
        /// <param name="value">The key.</param>
        /// <param name="length">The length.</param>
        /// <returns>The key.</returns>
        private static string Truncate(string value, int length)
        {
            return value.Length <= length ? value : value[..length];
        }

        /// <summary>
        /// Releases resources held by this manager.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
