using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="SlaScope"/>: which agreements of a class an object is
    /// held to.
    /// </summary>
    /// <remarks>
    /// Before the scope rules were evaluated every active agreement of a class ran on every
    /// object of it - an incident showed the clocks of all four priority agreements. These tests
    /// pin the rule semantics down: same type is an alternative, different types all have to
    /// hold, and what the object cannot carry does not restrict.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestSlaScope
    {
        private static readonly Guid WorkspaceId = Guid.Parse("5C0FE000-1111-4111-8111-000000000001");
        private static readonly Guid ClassId = Guid.Parse("5C0FE000-1111-4111-8111-000000000002");
        private static readonly Guid PriorityFieldId = Guid.Parse("5C0FE000-1111-4111-8111-000000000003");
        private static readonly Guid P1Id = Guid.Parse("5C0FE000-1111-4111-8111-000000000004");
        private static readonly Guid P2Id = Guid.Parse("5C0FE000-1111-4111-8111-000000000005");
        private static readonly Guid CriticalObjectId = Guid.Parse("5C0FE000-1111-4111-8111-000000000006");
        private static readonly Guid UnrankedObjectId = Guid.Parse("5C0FE000-1111-4111-8111-000000000007");

        /// <summary>
        /// Seeds a class with a two-step priority scale and a priority field, one object ranked
        /// P1 and tagged <c>vip</c>, and one object without a priority.
        /// </summary>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-scope", Name = "scope" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            db.Priorities.Add(new Priority { Id = P1Id, Name = "P1 - Critical", Order = 0, ClassId = ClassId, State = PriorityState.Active });
            db.Priorities.Add(new Priority { Id = P2Id, Name = "P2 - High", Order = 1, ClassId = ClassId, State = PriorityState.Active });
            db.Fields.Add(new Field { Id = PriorityFieldId, Name = "Priority", FieldType = FieldType.Priority, ClassId = ClassId, State = FieldState.Active });
            db.Objects.Add(new ObjectEntity { Id = CriticalObjectId, Key = "SC-1", Summary = "critical", ClassId = ClassId, WorkspaceId = WorkspaceId });
            db.Objects.Add(new ObjectEntity { Id = UnrankedObjectId, Key = "SC-2", Summary = "unranked", ClassId = ClassId, WorkspaceId = WorkspaceId });
            db.Values.Add(new Value { Id = Guid.NewGuid(), ObjectId = CriticalObjectId, FieldId = PriorityFieldId, Data = "P1 - Critical" });
            db.ObjectTags.Add(new ObjectTag { Id = Guid.NewGuid(), ObjectId = CriticalObjectId, Name = "vip" });
            db.SaveChanges();
        }

        /// <summary>
        /// Builds a policy with the given scope rules.
        /// </summary>
        private static SlaPolicy Policy(string name, params (SlaScopeRuleType Type, string Value)[] rules)
        {
            return new SlaPolicy
            {
                Id = Guid.NewGuid(),
                Name = name,
                ClassId = ClassId,
                State = SlaPolicyState.Active,
                Scope = [.. rules.Select(x => new SlaScopeRule { Id = Guid.NewGuid(), RuleType = x.Type, Value = x.Value })]
            };
        }

        /// <summary>
        /// Selects among the policies for the object, reading it through the hub's managers.
        /// </summary>
        private static List<string> Select(Guid objectId, params SlaPolicy[] policies)
        {
            var @object = CoreHub.ObjectManager.GetObject(objectId);

            return [.. SlaScope
                .Select(policies, @object, CoreHub.FieldManager, CoreHub.ValueManager, CoreHub.PriorityManager, CoreHub.ObjectTagManager)
                .Select(x => x.Name)];
        }

        /// <summary>
        /// An object is held to the agreement of its own priority only - the fault this fixes.
        /// </summary>
        [Fact]
        public void PriorityRuleSelectsTheAgreementOfTheObjectsPriority()
        {
            Seed(nameof(PriorityRuleSelectsTheAgreementOfTheObjectsPriority));

            var selected = Select
            (
                CriticalObjectId,
                Policy("P1", (SlaScopeRuleType.Priority, "P1 - Critical")),
                Policy("P2", (SlaScopeRuleType.Priority, "P2 - High"))
            );

            Assert.Equal(["P1"], selected);
        }

        /// <summary>
        /// A priority is compared ignoring case and surrounding blanks, the way names are
        /// compared elsewhere in the product.
        /// </summary>
        [Fact]
        public void PriorityIsComparedIgnoringCase()
        {
            Seed(nameof(PriorityIsComparedIgnoringCase));

            Assert.Equal(["P1"], Select(CriticalObjectId, Policy("P1", (SlaScopeRuleType.Priority, "  p1 - critical "))));
        }

        /// <summary>
        /// Rules of the same type are alternatives: an agreement for P1 or P2 covers a P1 object.
        /// </summary>
        [Fact]
        public void RulesOfOneTypeAreAlternatives()
        {
            Seed(nameof(RulesOfOneTypeAreAlternatives));

            var policy = Policy("P1 or P2", (SlaScopeRuleType.Priority, "P1 - Critical"), (SlaScopeRuleType.Priority, "P2 - High"));

            Assert.Equal(["P1 or P2"], Select(CriticalObjectId, policy));
        }

        /// <summary>
        /// Rules of different types all have to hold: a P1 agreement for VIPs covers the P1 VIP
        /// object, and one for another tag does not.
        /// </summary>
        [Fact]
        public void RulesOfDifferentTypesAllHaveToHold()
        {
            Seed(nameof(RulesOfDifferentTypesAllHaveToHold));

            var selected = Select
            (
                CriticalObjectId,
                Policy("P1 VIP", (SlaScopeRuleType.Priority, "P1 - Critical"), (SlaScopeRuleType.Tag, "VIP")),
                Policy("P1 partner", (SlaScopeRuleType.Priority, "P1 - Critical"), (SlaScopeRuleType.Tag, "partner"))
            );

            Assert.Equal(["P1 VIP"], selected);
        }

        /// <summary>
        /// An object without a priority is held to no priority-scoped agreement, and still to
        /// one without rules.
        /// </summary>
        [Fact]
        public void AnObjectWithoutPriorityGetsOnlyUnscopedAgreements()
        {
            Seed(nameof(AnObjectWithoutPriorityGetsOnlyUnscopedAgreements));

            var selected = Select
            (
                UnrankedObjectId,
                Policy("P1", (SlaScopeRuleType.Priority, "P1 - Critical")),
                Policy("Everyone")
            );

            Assert.Equal(["Everyone"], selected);
        }

        /// <summary>
        /// A rule type the object carries no attribute for restricts nothing, so a policy stating
        /// a contract or a system is not silently retired.
        /// </summary>
        [Fact]
        public void RuleTypesAnObjectCannotCarryDoNotRestrict()
        {
            Seed(nameof(RuleTypesAnObjectCannotCarryDoNotRestrict));

            var policy = Policy("Enterprise P1", (SlaScopeRuleType.Priority, "P1 - Critical"), (SlaScopeRuleType.Contract, "Enterprise"), (SlaScopeRuleType.System, "Production"));

            Assert.Equal(["Enterprise P1"], Select(CriticalObjectId, policy));
        }

        /// <summary>
        /// A priority rule naming no priority of the class can never be satisfied - it is a
        /// configuration mistake, such as the seeded agreements scoped to <c>High</c> on a P1-P4
        /// scale - and is left out of the decision rather than switching its policy off.
        /// </summary>
        [Fact]
        public void APriorityTheClassDoesNotDefineIsIgnored()
        {
            Seed(nameof(APriorityTheClassDoesNotDefineIsIgnored));

            var selected = Select
            (
                UnrankedObjectId,
                Policy("Dangling", (SlaScopeRuleType.Priority, "High"))
            );

            Assert.Equal(["Dangling"], selected);
        }

        /// <summary>
        /// A priority field that stores the priority's id rather than its name still selects the
        /// agreement of that priority.
        /// </summary>
        [Fact]
        public void APriorityStoredByIdIsResolved()
        {
            Seed(nameof(APriorityStoredByIdIsResolved));

            using (var db = CoreHubFixture.CreateDbContext(nameof(APriorityStoredByIdIsResolved)))
            {
                db.Values.Add(new Value { Id = Guid.NewGuid(), ObjectId = UnrankedObjectId, FieldId = PriorityFieldId, Data = P2Id.ToString() });
                db.SaveChanges();
            }

            var selected = Select
            (
                UnrankedObjectId,
                Policy("P1", (SlaScopeRuleType.Priority, "P1 - Critical")),
                Policy("P2", (SlaScopeRuleType.Priority, "P2 - High"))
            );

            Assert.Equal(["P2"], selected);
        }

        /// <summary>
        /// Policies without any rule are returned as they are, without reading the object.
        /// </summary>
        [Fact]
        public void UnscopedPoliciesAllApply()
        {
            var policies = new[] { Policy("A"), Policy("B") };

            var selected = SlaScope.Select(policies, new ObjectEntity { Id = Guid.NewGuid(), ClassId = ClassId }, null, null, null, null);

            Assert.Equal(["A", "B"], selected.Select(x => x.Name));
        }
    }
}
