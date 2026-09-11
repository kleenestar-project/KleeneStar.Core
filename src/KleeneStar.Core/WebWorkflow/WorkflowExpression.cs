using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebWorkflow
{
    /// <summary>
    /// The condition a transition carries, written as a disjunctive normal form over rule keys:
    /// <c>a;b|c</c> reads as <em>(a and b) or c</em>.
    /// </summary>
    /// <remarks>
    /// The form is not a compromise, it is the point. A condition an administrator can write
    /// without a language - by picking terms into a group and adding a second group for the
    /// alternative - is exactly a disjunction of conjunctions, and that is what the framework's
    /// DNF control produces and reads back. Anything more expressive would need a parser, an
    /// editor and an error message for a sentence somebody mistyped; anything less could not say
    /// <em>either the assignee is set, or an administrator is making the move</em>.
    /// <para>
    /// The serialization is the framework's, character for character
    /// (<c>ControlFormInputValueDnf</c>): terms joined with <c>;</c>, groups with <c>|</c>. A
    /// single group is therefore the same string a plain multi-select produces, which is what
    /// lets a condition be widened later without touching what was stored.
    /// </para>
    /// </remarks>
    public static class WorkflowExpression
    {
        /// <summary>
        /// Separates the terms of one conjunction.
        /// </summary>
        public const char TermSeparator = ';';

        /// <summary>
        /// Separates the conjunctions of the disjunction.
        /// </summary>
        public const char GroupSeparator = '|';

        /// <summary>
        /// Reads an expression into its groups of terms.
        /// </summary>
        /// <param name="expression">The stored expression. May be null.</param>
        /// <returns>The conjunctions, each a list of keys. Empty when the expression says
        /// nothing.</returns>
        public static IReadOnlyList<IReadOnlyList<string>> Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return [];
            }

            return
            [
                .. expression
                    .Split(GroupSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(group => (IReadOnlyList<string>)
                    [
                        .. group
                            .Split(TermSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                    ])
                    .Where(group => group.Count > 0)
            ];
        }

        /// <summary>
        /// Writes groups of terms back into the stored form.
        /// </summary>
        /// <param name="groups">The conjunctions.</param>
        /// <returns>The expression, or an empty string when there is nothing to say.</returns>
        public static string Serialize(IEnumerable<IEnumerable<string>> groups)
        {
            var written = (groups ?? [])
                .Select(group => string.Join(TermSeparator, (group ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())))
                .Where(group => group.Length > 0);

            return string.Join(GroupSeparator, written);
        }

        /// <summary>
        /// Evaluates an expression against a decision made per term.
        /// </summary>
        /// <remarks>
        /// An expression that says nothing is <see langword="true"/>: a transition without a
        /// condition is unconditional, and reading silence as a refusal would make every
        /// workflow that predates the feature impassable.
        /// <para>
        /// A term whose rule is not registered - a plugin was uninstalled, a key was mistyped -
        /// is <see langword="false"/> rather than ignored, so the conjunction holding it fails
        /// instead of quietly meaning less than it says. The alternative reading would let an
        /// uninstalled plugin widen a condition.
        /// </para>
        /// </remarks>
        /// <param name="expression">The stored expression.</param>
        /// <param name="evaluate">Answers one term.</param>
        /// <returns><see langword="true"/> when the expression holds.</returns>
        public static bool Evaluate(string expression, Func<string, bool> evaluate)
        {
            var groups = Parse(expression);

            if (groups.Count == 0)
            {
                return true;
            }

            ArgumentNullException.ThrowIfNull(evaluate);

            return groups.Any(group => group.All(evaluate));
        }

        /// <summary>
        /// Returns the terms that have to become true for the expression to hold: the ones
        /// missing from the conjunction that is closest to being satisfied.
        /// </summary>
        /// <remarks>
        /// A refused transition has to say what to do next, and naming every unsatisfied term of
        /// every alternative would list work that nobody has to do - the alternatives are
        /// alternatives. The group with the fewest missing terms is the shortest way to the
        /// move, which is the one worth reporting; ties are broken by the order the groups were
        /// administered in, because that is the order the person who wrote them meant.
        /// </remarks>
        /// <param name="expression">The stored expression.</param>
        /// <param name="evaluate">Answers one term.</param>
        /// <returns>The keys of the terms to satisfy, empty when the expression already holds.</returns>
        public static IReadOnlyList<string> Missing(string expression, Func<string, bool> evaluate)
        {
            var groups = Parse(expression);

            if (groups.Count == 0)
            {
                return [];
            }

            ArgumentNullException.ThrowIfNull(evaluate);

            var answered = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            bool answer(string term) => answered.TryGetValue(term, out var value)
                ? value
                : answered[term] = evaluate(term);

            var candidates = groups
                .Select(group => (IReadOnlyList<string>)[.. group.Where(term => !answer(term))])
                .ToList();

            return candidates.Any(x => x.Count == 0)
                ? []
                : candidates.OrderBy(x => x.Count).First();
        }
    }
}
