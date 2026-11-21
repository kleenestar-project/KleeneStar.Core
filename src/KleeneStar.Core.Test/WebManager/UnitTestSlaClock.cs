using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebManager
{
    using ObjectEntity = KleeneStar.Model.Entities.Object;
    using Status = KleeneStar.Model.Entities.Status;

    /// <summary>
    /// Provides unit tests for <see cref="SlaClock"/> — the derivation of a running SLA
    /// clock from an object, a policy and a target. The derivation is pure, so the tests
    /// drive it with an explicit moment instead of the wall clock.
    /// </summary>
    public class UnitTestSlaClock
    {
        /// <summary>
        /// The moment the clocks are read at in these tests.
        /// </summary>
        private static readonly DateTime Now = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Unspecified);

        /// <summary>
        /// Tests whether a target is expressed in wall-clock time according to its unit.
        /// </summary>
        [Theory]
        [InlineData(SlaTargetUnit.Minutes, 30, 0.5d)]
        [InlineData(SlaTargetUnit.Hours, 4, 4d)]
        [InlineData(SlaTargetUnit.Days, 2, 48d)]
        [InlineData(SlaTargetUnit.BusinessDays, 2, 16d)]
        public void GetBudget_ConvertsUnit(SlaTargetUnit unit, int value, double expectedHours)
        {
            var budget = SlaClock.GetBudget(new SlaTarget { Unit = unit, TargetValue = value });

            Assert.Equal(TimeSpan.FromHours(expectedHours), budget);
        }

        /// <summary>
        /// Tests whether a target without a positive value leaves the agreement without time,
        /// which is what makes an unconfigured target impossible to miss on the card.
        /// </summary>
        [Fact]
        public void GetBudget_WithoutValue_IsZero()
        {
            Assert.Equal(TimeSpan.Zero, SlaClock.GetBudget(new SlaTarget { Unit = SlaTargetUnit.Hours, TargetValue = 0 }));
            Assert.Equal(TimeSpan.Zero, SlaClock.GetBudget(null!));
        }

        /// <summary>
        /// Tests whether the clock of an object in a running status counts from its creation,
        /// and stays on track while it is inside its budget.
        /// </summary>
        [Fact]
        public void Derive_Running_CountsFromCreation()
        {
            var definition = SlaClock.Derive(Object(Now.AddHours(-3)), Policy(), Target(4), Status("In progress", "In progress"), Now);
            var evaluation = SlaEvaluator.Evaluate(definition!, Now);

            Assert.Null(definition!.PausedSince);
            Assert.Null(definition.FulfilledCycle);
            Assert.Equal(TimeSpan.FromHours(3), evaluation.Elapsed);
            Assert.Equal(TimeSpan.FromHours(1), evaluation.Remaining);
            Assert.False(evaluation.IsPaused);
            Assert.Equal(TypeStatusSla.Fulfilled, evaluation.Status);
        }

        /// <summary>
        /// Tests whether an agreement past the default warning threshold - four fifths of its
        /// budget - reports as at risk while there is still time left.
        /// </summary>
        [Fact]
        public void Derive_PastWarningThreshold_IsAtRisk()
        {
            var definition = SlaClock.Derive(Object(Now.AddHours(-3.5)), Policy(), Target(4), null, Now);
            var evaluation = SlaEvaluator.Evaluate(definition!, Now);

            Assert.Equal(TimeSpan.FromMinutes(30), evaluation.Remaining);
            Assert.Equal(TypeStatusSla.AtRisk, evaluation.Status);
        }

        /// <summary>
        /// Tests whether an overrun agreement reports as violated.
        /// </summary>
        [Fact]
        public void Derive_Overrun_IsViolated()
        {
            var definition = SlaClock.Derive(Object(Now.AddHours(-6)), Policy(), Target(4), null, Now);

            Assert.Equal(TypeStatusSla.Violated, SlaEvaluator.Evaluate(definition!, Now).Status);
        }

        /// <summary>
        /// Tests whether a status named in the policy's pause list stops the clock at the
        /// moment the object was last stamped, rather than letting it run on.
        /// </summary>
        [Fact]
        public void Derive_PauseOnStatus_StopsClockAtLastUpdate()
        {
            var @object = Object(Now.AddHours(-3), Now.AddHours(-1));
            var definition = SlaClock.Derive(@object, Policy("Waiting for customer"), Target(4), Status("Waiting for customer"), Now);
            var evaluation = SlaEvaluator.Evaluate(definition!, Now);

            Assert.Equal(Now.AddHours(-1), definition!.PausedSince);
            Assert.True(evaluation.IsPaused);
            Assert.Equal(TimeSpan.FromHours(2), evaluation.Elapsed);
            Assert.Equal(TypeStatusSla.Paused, evaluation.Status);
        }

        /// <summary>
        /// Tests whether the pause list is read as a comma-separated list of status names,
        /// regardless of the spacing and casing it was entered with.
        /// </summary>
        [Theory]
        [InlineData("Waiting for customer", true)]
        [InlineData("  waiting FOR customer , On hold ", true)]
        [InlineData("On hold", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsPaused_MatchesStatusName(string pauseOn, bool expected)
        {
            Assert.Equal(expected, SlaClock.IsPaused(Policy(pauseOn!), Status("Waiting for customer")));
        }

        /// <summary>
        /// Tests whether a status of the done category settles the agreement, and freezes its
        /// clock at the moment the object reached that status.
        /// </summary>
        [Fact]
        public void Derive_DoneCategory_SettlesAgreement()
        {
            var @object = Object(Now.AddHours(-6), Now.AddHours(-2));
            var definition = SlaClock.Derive(@object, Policy(), Target(4), Status("Closed", "Done"), Now);
            var evaluation = SlaEvaluator.Evaluate(definition!, Now);

            Assert.Equal(Now.AddHours(-2), definition!.FulfilledAt);
            Assert.True(evaluation.IsSettled);
            Assert.Equal(TimeSpan.FromHours(4), evaluation.Elapsed);

            // the settlement outranks the overrun the elapsed time would otherwise report
            Assert.Equal(TypeStatusSla.Fulfilled, evaluation.Status);
        }

        /// <summary>
        /// Tests whether the done category is recognised regardless of how it is spaced or
        /// cased, and whether every other category leaves the agreement open.
        /// </summary>
        [Theory]
        [InlineData("Done", true)]
        [InlineData(" done ", true)]
        [InlineData("In progress", false)]
        [InlineData(null, false)]
        public void IsSettled_ReadsCategory(string category, bool expected)
        {
            Assert.Equal(expected, SlaClock.IsSettled(Status("Closed", category!)));
        }

        /// <summary>
        /// Tests whether a stamp that predates the creation of the object is pulled up to the
        /// start of the clock, so a paused agreement cannot report a negative elapsed time.
        /// </summary>
        [Fact]
        public void Derive_StampBeforeCreation_ClampsToStart()
        {
            var @object = Object(Now.AddHours(-3), Now.AddHours(-5));
            var definition = SlaClock.Derive(@object, Policy("Waiting for customer"), Target(4), Status("Waiting for customer"), Now);

            Assert.Equal(@object.Created, definition!.PausedSince);
            Assert.Equal(TimeSpan.Zero, SlaEvaluator.Evaluate(definition, Now).Elapsed);
        }

        /// <summary>
        /// Tests whether a derivation without an object or without a target yields no clock.
        /// </summary>
        [Fact]
        public void Derive_WithoutObjectOrTarget_IsNull()
        {
            Assert.Null(SlaClock.Derive(null!, Policy(), Target(4), null, Now));
            Assert.Null(SlaClock.Derive(Object(Now), Policy(), null!, null, Now));
        }

        /// <summary>
        /// Builds an object created at the given moment and stamped at the given moment.
        /// </summary>
        /// <param name="created">The creation moment.</param>
        /// <param name="updated">The last update, defaulting to the creation moment.</param>
        /// <returns>The object.</returns>
        private static ObjectEntity Object(DateTime created, DateTime? updated = null)
        {
            return new ObjectEntity { Created = created, Updated = updated ?? created };
        }

        /// <summary>
        /// Builds a policy with the given pause statuses.
        /// </summary>
        /// <param name="pauseOn">The comma-separated pause statuses.</param>
        /// <returns>The policy.</returns>
        private static SlaPolicy Policy(string pauseOn = null)
        {
            return new SlaPolicy { Name = "Incident", State = SlaPolicyState.Active, PauseOn = pauseOn };
        }

        /// <summary>
        /// Builds a resolution target of the given number of hours.
        /// </summary>
        /// <param name="hours">The budget in hours.</param>
        /// <returns>The target.</returns>
        private static SlaTarget Target(int hours)
        {
            return new SlaTarget { Kind = SlaTargetKind.Resolution, TargetValue = hours, Unit = SlaTargetUnit.Hours };
        }

        /// <summary>
        /// Builds a status of the given name and category.
        /// </summary>
        /// <param name="name">The status name.</param>
        /// <param name="category">The category name, or null for a status without one.</param>
        /// <returns>The status.</returns>
        private static Status Status(string name, string category = null)
        {
            return new Status
            {
                Name = name,
                Category = category is null ? null : new StatusCategory { Name = category }
            };
        }
    }
}
