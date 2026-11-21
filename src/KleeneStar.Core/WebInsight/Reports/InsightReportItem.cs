using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebInsight.Reports
{
    /// <summary>
    /// One state an object was in from a point in time on: the status category its workflow
    /// value resolved to.
    /// </summary>
    /// <param name="At">When the object entered the state (UTC).</param>
    /// <param name="Category">The normalized key of the status category, for example <c>inprogress</c>.</param>
    public sealed record InsightReportState(DateTime At, string Category);

    /// <summary>
    /// An object as the reports see it: when it was created, which status categories it went
    /// through and when, and what it is planned and estimated as. The reports are computed from
    /// these alone, so they can be checked without a store.
    /// </summary>
    /// <remarks>
    /// The states are replayed from the commit chain of the object's workflow field; the reports
    /// work on status <em>categories</em> rather than statuses, because the insight spans classes
    /// whose workflows name their statuses differently, while every status belongs to one of the
    /// shared categories (to do, in progress, waiting, done).
    /// </remarks>
    public sealed class InsightReportItem
    {
        /// <summary>
        /// The key of the category that counts as resolved.
        /// </summary>
        public const string Done = "done";

        /// <summary>
        /// Gets the id of the object.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        /// Gets when the object was created (UTC).
        /// </summary>
        public DateTime Created { get; init; }

        /// <summary>
        /// Gets the states of the object, oldest first. The first one starts at
        /// <see cref="Created"/>; an object whose class has no workflow has none.
        /// </summary>
        public IReadOnlyList<InsightReportState> States { get; init; } = [];

        /// <summary>
        /// Gets the story points the object is estimated at, if any.
        /// </summary>
        public int? StoryPoints { get; init; }

        /// <summary>
        /// Gets the sprint the object is planned in, if any.
        /// </summary>
        public Guid? SprintId { get; init; }

        /// <summary>
        /// Gets a value indicating whether the object has a state at all - whether its class has
        /// a workflow. The reports count only such objects: an object without a state is never
        /// resolved, and would age forever.
        /// </summary>
        public bool HasStates => States.Count > 0;

        /// <summary>
        /// Returns the category the object was in at a point in time.
        /// </summary>
        /// <param name="at">The point in time (UTC).</param>
        /// <returns>The category key, or <see langword="null"/> before the object existed or for an object without states.</returns>
        public string CategoryAt(DateTime at)
        {
            if (!HasStates || at < Created)
            {
                return null;
            }

            string category = States[0].Category;

            foreach (var state in States)
            {
                if (state.At > at)
                {
                    break;
                }

                category = state.Category;
            }

            return category;
        }

        /// <summary>
        /// Determines whether the object was open - existing and not resolved - at a point in time.
        /// </summary>
        /// <param name="at">The point in time (UTC).</param>
        /// <returns><see langword="true"/> when it was open.</returns>
        public bool IsOpenAt(DateTime at)
        {
            var category = CategoryAt(at);

            return category is not null && category != Done;
        }

        /// <summary>
        /// Returns the moments the object was resolved: every entry into the done category from
        /// another one, and its creation when it started out done. An object reopened and
        /// resolved again is resolved twice.
        /// </summary>
        /// <returns>The resolution moments, oldest first.</returns>
        public IEnumerable<DateTime> Resolutions()
        {
            string previous = null;

            foreach (var state in States)
            {
                if (state.Category == Done && previous != Done)
                {
                    yield return state.At < Created ? Created : state.At;
                }

                previous = state.Category;
            }
        }

        /// <summary>
        /// Builds the states of an object from the categories its workflow value resolved to,
        /// dropping a state that repeats the one before it.
        /// </summary>
        /// <param name="created">When the object was created.</param>
        /// <param name="states">The states in time order; the first is taken to start at creation.</param>
        /// <returns>The normalized states.</returns>
        public static IReadOnlyList<InsightReportState> Normalize(DateTime created, IEnumerable<InsightReportState> states)
        {
            var result = new List<InsightReportState>();

            foreach (var state in (states ?? []).Where(x => !string.IsNullOrEmpty(x.Category)).OrderBy(x => x.At))
            {
                var at = result.Count == 0 || state.At < created ? created : state.At;

                if (result.Count > 0 && result[^1].Category == state.Category)
                {
                    continue;
                }

                // a later change recorded at the same moment as the one before it replaces it
                if (result.Count > 0 && result[^1].At == at)
                {
                    result[^1] = new InsightReportState(at, state.Category);

                    if (result.Count > 1 && result[^2].Category == state.Category)
                    {
                        result.RemoveAt(result.Count - 1);
                    }

                    continue;
                }

                result.Add(new InsightReportState(at, state.Category));
            }

            return result;
        }
    }
}
