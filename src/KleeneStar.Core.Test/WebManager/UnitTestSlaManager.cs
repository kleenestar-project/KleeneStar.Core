using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.SlaManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestSlaManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E11D9C28-2F7E-4DBA-94CC-7A9B3D7F4D11");
        private static readonly Guid ClassId = Guid.Parse("9E1F73C8-50D6-4F4B-9A98-E7D9C2A8BE25");

        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-sla", Name = "workspace" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.SlaManager.Add"/> persists the policy and that
        /// <see cref="KleeneStar.Core.WebManager.SlaManager.GetSla"/> retrieves it with its children.
        /// </summary>
        [Fact]
        public void Add_Then_GetSla_RoundTrip()
        {
            // arrange
            Seed(nameof(Add_Then_GetSla_RoundTrip));

            var policy = SamplePolicy();

            // act
            CoreHub.SlaManager.Add(policy);
            var loaded = CoreHub.SlaManager.GetSla(policy.Id);

            // validation
            Assert.NotNull(loaded);
            Assert.Equal(policy.Name, loaded.Name);
            Assert.Equal(2, loaded.Targets.Count);
            Assert.Equal(2, loaded.Scope.Count);
            Assert.Single(loaded.Escalations);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.SlaManager.GetSlas(ClassIdParameter)"/> returns
        /// all policies belonging to a class.
        /// </summary>
        [Fact]
        public void GetSlas_ByClassId_ReturnsPoliciesForClass()
        {
            // arrange
            Seed(nameof(GetSlas_ByClassId_ReturnsPoliciesForClass));

            CoreHub.SlaManager.Add(SamplePolicy("Alpha"));
            CoreHub.SlaManager.Add(SamplePolicy("Beta"));

            // act
            var result = CoreHub.SlaManager.GetSlas(new ClassIdParameter(ClassId)).ToList();

            // validation
            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.Name == "Alpha");
            Assert.Contains(result, p => p.Name == "Beta");
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.SlaManager.Update"/> changes scalar properties
        /// and replaces the child collections.
        /// </summary>
        [Fact]
        public void Update_Policy_ReplacesChildren()
        {
            // arrange
            Seed(nameof(Update_Policy_ReplacesChildren));

            var policy = SamplePolicy("Initial");
            CoreHub.SlaManager.Add(policy);

            // act
            policy.Name = "Renamed";
            policy.Priority = SlaPriority.Medium;
            policy.Targets.Clear();
            policy.Targets.Add(new SlaTarget { Name = "Resp", Kind = SlaTargetKind.Response, TargetValue = 2, Unit = SlaTargetUnit.Hours });
            policy.Escalations.Clear();

            CoreHub.SlaManager.Update(policy);
            var loaded = CoreHub.SlaManager.GetSla(policy.Id);

            // validation
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal(SlaPriority.Medium, loaded.Priority);
            Assert.Single(loaded.Targets);
            Assert.Empty(loaded.Escalations);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.SlaManager.Remove"/> deletes the policy and
        /// raises the <see cref="KleeneStar.Core.WebManager.ISlaManager.SlaRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_Policy_DeletesItAndRaisesEvent()
        {
            // arrange
            Seed(nameof(Remove_Policy_DeletesItAndRaisesEvent));

            var policy = SamplePolicy();
            CoreHub.SlaManager.Add(policy);

            SlaPolicy raised = null;
            CoreHub.SlaManager.SlaRemoved += (_, p) => raised = p;

            // act
            CoreHub.SlaManager.Remove(policy.Id);

            // validation
            Assert.Null(CoreHub.SlaManager.GetSla(policy.Id));
            Assert.NotNull(raised);
            Assert.Equal(policy.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.SlaManager.Remove"/> is a no-op when the
        /// policy does not exist.
        /// </summary>
        [Fact]
        public void Remove_UnknownPolicy_IsNoOp()
        {
            // arrange
            Seed(nameof(Remove_UnknownPolicy_IsNoOp));

            // act + validation (no exception)
            CoreHub.SlaManager.Remove(Guid.NewGuid());

            Assert.Empty(CoreHub.SlaManager.GetSlas(ClassId));
        }

        /// <summary>
        /// Verifies that the <see cref="KleeneStar.Core.WebManager.SlaManager.ReservedSlaNames"/> set
        /// blocks well-known URL segments from being used as policy ids.
        /// </summary>
        [Fact]
        public void ReservedSlaNames_BlocksRouterSegments()
        {
            Assert.Contains("add", KleeneStar.Core.WebManager.SlaManager.ReservedSlaNames);
            Assert.Contains("edit", KleeneStar.Core.WebManager.SlaManager.ReservedSlaNames);
            Assert.Contains("delete", KleeneStar.Core.WebManager.SlaManager.ReservedSlaNames);
            Assert.Contains("api", KleeneStar.Core.WebManager.SlaManager.ReservedSlaNames);
        }

        /// <summary>
        /// Creates a sample SLA policy for testing purposes.
        /// </summary>
        /// <param name="name">The name of the policy.</param>
        /// <returns>The sample SLA policy.</returns>
        private static SlaPolicy SamplePolicy(string name = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = name ?? "P1 · Enterprise",
            ClassId = ClassId,
            State = SlaPolicyState.Active,
            Priority = SlaPriority.Critical,
            CalendarId = null,
            Notifications = SlaNotificationChannels.Email | SlaNotificationChannels.InApp,
            Targets =
            {
                new SlaTarget { Name = "First response", Kind = SlaTargetKind.Response,   TargetValue = 30, Unit = SlaTargetUnit.Minutes },
                new SlaTarget { Name = "Resolution",     Kind = SlaTargetKind.Resolution, TargetValue = 4,  Unit = SlaTargetUnit.Hours },
            },
            Scope =
            {
                new SlaScopeRule { RuleType = SlaScopeRuleType.Priority, Value = "High" },
                new SlaScopeRule { RuleType = SlaScopeRuleType.Contract, Value = "Enterprise" },
            },
            Escalations =
            {
                new SlaEscalationLevel { AfterValue = 15, Unit = SlaTargetUnit.Minutes, Notify = "Team Lead" },
            }
        };
    }
}
