using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests the automation platform: rules queue a job per matching event, exactly once per
    /// occurrence; the queue runs the script, retries a failure with a growing delay and parks
    /// it after the last attempt; a lease that ran out is claimed again; a chain of rules
    /// reacting to each other stops; scheduled rules run once per interval.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAutomationManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A070A000-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("A070A000-0000-4000-8000-000000000002");
        private static readonly Guid OtherClassId = Guid.Parse("A070A000-0000-4000-8000-000000000003");
        private static readonly Guid AgentId = Guid.Parse("A070A000-0000-4000-8000-000000000004");
        private static readonly Guid ImpactFieldId = Guid.Parse("A070A000-0000-4000-8000-000000000005");

        /// <summary>
        /// Seeds a workspace with two classes, a text field and an agent.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "OPS", Name = "Operations" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            db.Classes.Add(new Class { Id = OtherClassId, Name = "Change", WorkspaceId = WorkspaceId });
            db.Fields.Add(new Field { Id = ImpactFieldId, Name = "Impact", ClassId = ClassId, FieldType = FieldType.Text, State = FieldState.Active });
            db.Identities.Add(new Identity { Id = AgentId, Name = "Agent", UserName = "agent", Email = "agent@example.com", PasswordHash = "x" });
            db.SaveChanges();
        }

        /// <summary>
        /// Creates an object directly, without raising an event.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="classId">The class.</param>
        /// <param name="summary">The summary.</param>
        /// <returns>The object id.</returns>
        private static Guid Object(string database, Guid classId, string summary)
        {
            var id = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Objects.Add(new ObjectEntity(id) { Key = "OPS-" + Random.Shared.Next(1000, 9999), Summary = summary, WorkspaceId = WorkspaceId, ClassId = classId });
            db.SaveChanges();

            return id;
        }

        /// <summary>
        /// Adds a rule.
        /// </summary>
        /// <param name="trigger">The trigger.</param>
        /// <param name="actions">The script.</param>
        /// <param name="condition">The WQL condition, or <see langword="null"/>.</param>
        /// <param name="parameter">The trigger parameter, or <see langword="null"/>.</param>
        /// <param name="classId">The class, or <see langword="null"/>.</param>
        /// <returns>The rule.</returns>
        private static AutomationRule Rule(string trigger, string actions, string condition = null, string parameter = null, Guid? classId = null)
        {
            var rule = new AutomationRule
            {
                Name = "rule " + Guid.NewGuid().ToString("N")[..6],
                WorkspaceId = WorkspaceId,
                ClassId = classId,
                Trigger = trigger,
                TriggerParameter = parameter,
                Condition = condition,
                Actions = actions,
                RunAsId = AgentId,
                State = AutomationRuleState.Active
            };

            CoreHub.AutomationManager.AddRule(rule);

            return rule;
        }

        /// <summary>
        /// Returns the jobs of a rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The jobs.</returns>
        private static IReadOnlyList<AutomationJob> JobsOf(AutomationRule rule)
        {
            return ModelHub.GetAutomationJobs([rule.Id], null, 100);
        }

        /// <summary>
        /// An event queues one job per matching rule, and a second delivery of the same occurrence
        /// queues none; a rule of another trigger, another class, a failing condition or a
        /// disabled rule queues nothing.
        /// </summary>
        [Fact]
        public void Raise_QueuesOncePerOccurrence()
        {
            var database = nameof(Raise_QueuesOncePerOccurrence);
            Seed(database);

            var objectId = Object(database, ClassId, "Mail server down");
            var matching = Rule(AutomationTriggerCatalog.ObjectCreated, "assign agent", condition: "Summary ~ \"mail\"");
            var otherTrigger = Rule(AutomationTriggerCatalog.CommentAdded, "assign agent");
            var otherClass = Rule(AutomationTriggerCatalog.ObjectCreated, "assign agent", classId: OtherClassId);
            var failingCondition = Rule(AutomationTriggerCatalog.ObjectCreated, "assign agent", condition: "Summary ~ \"printer\"");

            var @event = new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = objectId, Occurrence = "commit:1" };

            Assert.Equal(1, CoreHub.AutomationManager.Raise(@event));
            Assert.Equal(0, CoreHub.AutomationManager.Raise(@event));

            Assert.Single(JobsOf(matching));
            Assert.Empty(JobsOf(otherTrigger));
            Assert.Empty(JobsOf(otherClass));
            Assert.Empty(JobsOf(failingCondition));
        }

        /// <summary>
        /// The queue runs a job's script line by line and records the progress.
        /// </summary>
        [Fact]
        public void ProcessDue_RunsTheScript()
        {
            var database = nameof(ProcessDue_RunsTheScript);
            Seed(database);

            var objectId = Object(database, ClassId, "Outage");
            var rule = Rule(AutomationTriggerCatalog.ObjectCreated, "assign agent\n# set the impact\nset Impact=High\ncomment internal: Taken over by the rule.");

            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = objectId, Occurrence = "commit:2" });

            Assert.True(CoreHub.AutomationManager.ProcessDue() >= 1);

            var job = Assert.Single(JobsOf(rule));
            Assert.Equal(AutomationJobState.Succeeded, job.State);
            Assert.Equal(3, job.CompletedActions);

            Assert.Equal(AgentId, CoreHub.ObjectManager.GetObject(objectId).AssigneeId);
            Assert.Equal("High", CoreHub.ValueManager.GetValue(objectId, ImpactFieldId)?.Data);

            var comment = Assert.Single(CoreHub.CommentManager.GetComments(objectId));
            Assert.Equal(CommentVisibility.InternalTeam, comment.Visibility);
            Assert.Equal(AgentId, comment.AuthorId);
        }

        /// <summary>
        /// A failing job is retried with a growing delay, parked after the last attempt, and
        /// runs again once somebody retries it - after the actions it had already done.
        /// </summary>
        [Fact]
        public void FailingJob_IsRetriedThenParked()
        {
            var database = nameof(FailingJob_IsRetriedThenParked);
            Seed(database);

            var objectId = Object(database, ClassId, "Broken");
            var rule = Rule(AutomationTriggerCatalog.ObjectCreated, "set Impact=Low\ntransition Done");

            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = objectId, Occurrence = "commit:3" });

            // the class has no workflow, so the second line fails every time
            for (var attempt = 1; attempt <= AutomationManager.MaxAttempts; attempt++)
            {
                var job = Assert.Single(JobsOf(rule));

                // pull the next attempt forward instead of waiting for the delay
                job.NextAttempt = DateTime.UtcNow.AddSeconds(-1);
                ModelHub.UpdateAutomationJob(job);

                CoreHub.AutomationManager.ProcessDue();
            }

            var parked = Assert.Single(JobsOf(rule));
            Assert.Equal(AutomationJobState.Failed, parked.State);
            Assert.Equal(1, parked.CompletedActions);
            Assert.Contains("workflow", parked.LastError, StringComparison.OrdinalIgnoreCase);

            var retried = CoreHub.AutomationManager.Retry(parked.Id);
            Assert.Equal(AutomationJobState.Pending, retried.State);
            Assert.Equal(0, retried.Attempts);
            Assert.Equal(1, retried.CompletedActions);
        }

        /// <summary>
        /// The delay before a retry grows with every attempt.
        /// </summary>
        [Fact]
        public void Backoff_Grows()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), AutomationManager.Backoff(1));
            Assert.Equal(TimeSpan.FromMinutes(2), AutomationManager.Backoff(2));
            Assert.True(AutomationManager.Backoff(4) > AutomationManager.Backoff(3));
        }

        /// <summary>
        /// A job held by a worker that died is claimed again once its lease ran out, and not
        /// before.
        /// </summary>
        [Fact]
        public void ExpiredLease_IsClaimedAgain()
        {
            var database = nameof(ExpiredLease_IsClaimedAgain);
            Seed(database);

            var objectId = Object(database, ClassId, "Lease");
            var rule = Rule(AutomationTriggerCatalog.ObjectCreated, "set Impact=Medium");

            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = objectId, Occurrence = "commit:4" });

            var claimed = Assert.Single(ModelHub.ClaimAutomationJobs("dead-worker", DateTime.UtcNow, TimeSpan.FromMinutes(5), 10));
            Assert.Empty(ModelHub.ClaimAutomationJobs("other-worker", DateTime.UtcNow, TimeSpan.FromMinutes(5), 10));

            var later = DateTime.UtcNow.AddMinutes(10);
            var reclaimed = Assert.Single(ModelHub.ClaimAutomationJobs("other-worker", later, TimeSpan.FromMinutes(5), 10));

            Assert.Equal(claimed.Id, reclaimed.Id);
            Assert.Equal("other-worker", reclaimed.LockedBy);
            Assert.Equal(2, reclaimed.Attempts);
        }

        /// <summary>
        /// What a rule does does not trigger the same rule again: a rule reacting to every change
        /// by changing the object runs once, not forever.
        /// </summary>
        [Fact]
        public void Rule_DoesNotTriggerItself()
        {
            var database = nameof(Rule_DoesNotTriggerItself);
            Seed(database);
            CoreHub.AutomationManager.Connect();

            var objectId = Object(database, ClassId, "Loop");
            var rule = Rule(AutomationTriggerCatalog.ObjectUpdated, "set Impact=Critical");

            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectUpdated, ObjectId = objectId, Occurrence = "commit:5" });

            CoreHub.AutomationManager.ProcessDue();
            CoreHub.AutomationManager.ProcessDue();

            Assert.Single(JobsOf(rule));
            Assert.Equal("Critical", CoreHub.ValueManager.GetValue(objectId, ImpactFieldId)?.Data);
        }

        /// <summary>
        /// A scheduled rule queues one job per object of its scope per interval, however often
        /// the scheduler looks.
        /// </summary>
        [Fact]
        public void Schedule_QueuesOncePerInterval()
        {
            var database = nameof(Schedule_QueuesOncePerInterval);
            Seed(database);

            Object(database, ClassId, "Stale one");
            Object(database, ClassId, "Stale two");

            var rule = Rule(AutomationTriggerCatalog.Schedule, "escalate", parameter: "60", classId: ClassId);
            var now = DateTime.UtcNow;

            Assert.Equal(2, CoreHub.AutomationManager.RunSchedules(now));

            // the rule's next run lies an interval ahead
            Assert.Equal(0, CoreHub.AutomationManager.RunSchedules(now.AddMinutes(1)));
            Assert.Equal(2, JobsOf(rule).Count);
        }

        /// <summary>
        /// A rule is validated in full before it is stored: an unknown action, a wrong argument,
        /// a broken condition and a schedule without interval are refused with their lines.
        /// </summary>
        [Fact]
        public void Validate_ReportsEveryProblem()
        {
            Seed(nameof(Validate_ReportsEveryProblem));

            var problems = CoreHub.AutomationManager.Validate(new AutomationRule
            {
                Name = "broken",
                WorkspaceId = WorkspaceId,
                ClassId = ClassId,
                Trigger = AutomationTriggerCatalog.Schedule,
                Condition = "Summary ~~ ",
                Actions = "teleport now\nset Missing=1\nassign nobody-known"
            });

            Assert.Contains(problems, x => x.Contains("interval", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(problems, x => x.Contains("WQL", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 1:", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 2:", StringComparison.Ordinal));
            Assert.Contains(problems, x => x.StartsWith("Line 3:", StringComparison.Ordinal));

            Assert.Throws<ArgumentException>(() => CoreHub.AutomationManager.AddRule(new AutomationRule { Name = "x", WorkspaceId = WorkspaceId, Trigger = "nothing", Actions = "assign agent" }));
        }

        /// <summary>
        /// `archive` archives at once; `archive 90` leaves an object that is not done alone,
        /// and a number that is no number is refused when the rule is saved.
        /// </summary>
        [Fact]
        public void Archive_KeepsWhatIsNotDueYet()
        {
            var database = nameof(Archive_KeepsWhatIsNotDueYet);
            Seed(database);

            var now = Object(database, ClassId, "Archive now");
            var later = Object(database, ClassId, "Not done yet");
            var direct = Rule(AutomationTriggerCatalog.ObjectCreated, "archive", condition: "Summary ~ \"now\"");
            var retention = Rule(AutomationTriggerCatalog.ObjectCreated, "archive 90", condition: "Summary ~ \"yet\"");

            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = now, Occurrence = "commit:archive-1" });
            CoreHub.AutomationManager.Raise(new AutomationEvent { Trigger = AutomationTriggerCatalog.ObjectCreated, ObjectId = later, Occurrence = "commit:archive-2" });
            CoreHub.AutomationManager.ProcessDue();

            Assert.Equal(AutomationJobState.Succeeded, Assert.Single(JobsOf(direct)).State);
            Assert.Equal(AutomationJobState.Succeeded, Assert.Single(JobsOf(retention)).State);

            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                Assert.Equal(WorkspaceState.Archived, CoreHub.ObjectManager.GetObject(now).State);
                Assert.Equal(WorkspaceState.Active, CoreHub.ObjectManager.GetObject(later).State);
            }

            Assert.Contains(CoreHub.AutomationManager.Validate(new AutomationRule { Name = "x", WorkspaceId = WorkspaceId, Trigger = AutomationTriggerCatalog.ObjectCreated, Actions = "archive soon" }), x => x.StartsWith("Line 1:", StringComparison.Ordinal));
        }
    }
}
