using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing insights, including adding, retrieving, and removing, as well as
    /// handling insight-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing insights and events for tracking changes 
    /// to the insight collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public interface IInsightManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an insight is added.
        /// </summary>
        event EventHandler<Insight> InsightAdded;

        /// <summary>
        /// An event that fires when an insight is updated.
        /// </summary>
        event EventHandler<Insight> InsightUpdated;

        /// <summary>
        /// An event that fires when an insight is removed.
        /// </summary>
        event EventHandler<Insight> InsightRemoved;

        /// <summary>
        /// Returns an insight based on its id.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <returns>The insight.</returns>
        Insight GetInsight(Guid insightId);

        /// <summary>
        /// Returns an insight based on its id.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <returns>The insight.</returns>
        Insight GetInsight(InsightIdParameter insightId);

        /// <summary>
        /// Retrieves a collection of insights that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned insights. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of insights that match the given predicate. If no insights 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Insight> GetInsights(IQuery<Insight> query);

        /// <summary>
        /// Retrieves a collection of insights that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned insights. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of insights that match the given predicate. If no insights 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Insight> GetInsights(IQuery<Insight> query, IQueryContext context);

        /// <summary>
        /// Adds an insight to the manager.
        /// </summary>
        /// <param name="insight">The insight to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IInsightManager Add(Insight insight);

        /// <summary>
        /// Updates an insight in the manager.
        /// </summary>
        /// <param name="insight">The insight to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IInsightManager Update(Insight insight);

        /// <summary>
        /// Removes the specified insight from the manager.
        /// </summary>
        /// <remarks>This method removes the specified insight from the manager. If the insight does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="insightId">The insight id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IInsightManager Remove(Guid insightId);

        /// <summary>
        /// Applies a column-only layout change (add, rename, resize, recolor, reorder, delete) to a
        /// insight while preserving the widgets of the surviving columns.
        /// </summary>
        /// <remarks>
        /// This persists the change silently (without raising a user notification) because it backs
        /// the insight's live autosave, and raises <see cref="InsightUpdated"/> when the insight
        /// exists. The list order defines the persisted column order; columns carrying
        /// <see cref="Guid.Empty"/> (or an unknown id) are created, and existing columns absent from
        /// the set are removed together with their widgets.
        /// </remarks>
        /// <param name="insightId">The id of the insight to update.</param>
        /// <param name="columns">
        /// The desired columns in their target order. Widgets on these instances are ignored. Must not
        /// be null.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IInsightManager SetColumns(Guid insightId, IReadOnlyList<DashboardColumn> columns);

        /// <summary>
        /// Applies a full board update (a widget being added, deleted, reconfigured or moved) to a
        /// insight, rebuilding the widgets of every column from the desired state.
        /// </summary>
        /// <remarks>
        /// This persists the change silently (without raising a user notification) because it backs
        /// the insight's live autosave, and raises <see cref="InsightUpdated"/> when the insight
        /// exists. Columns are reconciled as in <see cref="SetColumns"/>; in addition the widgets of
        /// every surviving or created column are recreated from the desired widgets, with the list
        /// order defining their position.
        /// </remarks>
        /// <param name="insightId">The id of the insight to update.</param>
        /// <param name="columns">
        /// The desired columns, each carrying the widgets it should hold, in their target order. Must
        /// not be null.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IInsightManager SetBoard(Guid insightId, IReadOnlyList<DashboardColumn> columns);
    }
}
