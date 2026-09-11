using KleeneStar.Core.WebWorkflow;

namespace KleeneStar.Core.Test.WebWorkflow
{
    /// <summary>
    /// Provides unit tests for <see cref="WorkflowExpression"/> — the disjunctive normal form a
    /// transition carries its conditions in, and the shape the administering control produces.
    /// </summary>
    public class UnitTestWorkflowExpression
    {
        /// <summary>
        /// Verifies the reading of the two separators: terms are joined with a semicolon into a
        /// conjunction, conjunctions with a bar into the disjunction.
        /// </summary>
        [Fact]
        public void Parse_ReadsGroupsAndTerms()
        {
            var groups = WorkflowExpression.Parse("a;b|c");

            Assert.Equal(2, groups.Count);
            Assert.Equal(["a", "b"], groups[0]);
            Assert.Equal(["c"], groups[1]);
        }

        /// <summary>
        /// Verifies that noise is dropped rather than turned into terms: empty groups, blank
        /// terms and a term named twice in one conjunction say nothing more than the expression
        /// without them.
        /// </summary>
        [Fact]
        public void Parse_DropsEmptyAndRepeatedTerms()
        {
            var groups = WorkflowExpression.Parse(" a ; a ;; b || c ");

            Assert.Equal(2, groups.Count);
            Assert.Equal(["a", "b"], groups[0]);
            Assert.Equal(["c"], groups[1]);
        }

        /// <summary>
        /// Verifies that an expression saying nothing holds. A transition without a condition is
        /// unconditional, and reading silence as a refusal would make every workflow that
        /// predates the feature impassable.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Evaluate_EmptyExpression_Holds(string expression)
        {
            Assert.True(WorkflowExpression.Evaluate(expression, _ => false));
        }

        /// <summary>
        /// Verifies the logic itself: every term of one conjunction has to hold, and one holding
        /// conjunction is enough.
        /// </summary>
        [Fact]
        public void Evaluate_AndWithinGroups_OrBetweenThem()
        {
            static bool only(string term) => term is "a" or "c";

            // (a and b) or (c) - the first group fails on b, the second holds
            Assert.True(WorkflowExpression.Evaluate("a;b|c", only));

            // (a and b) alone does not hold
            Assert.False(WorkflowExpression.Evaluate("a;b", only));

            // (a) alone does
            Assert.True(WorkflowExpression.Evaluate("a", only));
        }

        /// <summary>
        /// Verifies that a term nobody can answer is false rather than ignored, so the
        /// conjunction holding it fails instead of quietly meaning less than it says — an
        /// uninstalled plugin must not widen a condition.
        /// </summary>
        [Fact]
        public void Evaluate_UnknownTerm_IsFalse()
        {
            Assert.False(WorkflowExpression.Evaluate("a;gone", term => term == "a"));
        }

        /// <summary>
        /// Verifies what a refusal reports: the terms of the alternative closest to being
        /// satisfied, because the alternatives are alternatives and naming all of them would
        /// list work nobody has to do.
        /// </summary>
        [Fact]
        public void Missing_NamesTheShortestWayThrough()
        {
            // (a and b and c) or (d): a holds, d does not - the second group is one term away,
            // the first two
            var missing = WorkflowExpression.Missing("a;b;c|d", term => term == "a");

            Assert.Equal(["d"], missing);
        }

        /// <summary>
        /// Verifies that a satisfied expression is missing nothing, whichever of its
        /// alternatives holds.
        /// </summary>
        [Fact]
        public void Missing_SatisfiedExpression_IsEmpty()
        {
            Assert.Empty(WorkflowExpression.Missing("a;b|c", term => term == "c"));
        }

        /// <summary>
        /// Verifies that writing and reading are the same operation in two directions, and that
        /// a single conjunction serializes to the semicolon list every plain selection control
        /// already speaks.
        /// </summary>
        [Fact]
        public void Serialize_RoundTrips()
        {
            Assert.Equal("a;b|c", WorkflowExpression.Serialize([["a", "b"], ["c"]]));
            Assert.Equal("a;b", WorkflowExpression.Serialize([["a", "b"]]));
            Assert.Equal("", WorkflowExpression.Serialize([[], [" "]]));
        }
    }
}
