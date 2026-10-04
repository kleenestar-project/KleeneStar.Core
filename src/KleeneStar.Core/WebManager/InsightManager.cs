using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages insights, including adding, retrieving, updating, and removing, as well as
    /// handling insight-related events.
    /// </summary>
    /// <remarks>
    /// The class provides methods for managing insights and events for tracking changes 
    /// to the insight collection. It ensures controlled lifecycle management and 
    /// data integrity for all insight entities.
    /// </remarks>
    public sealed class InsightManager : IInsightManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an insight is added.
        /// </summary>
        public event EventHandler<Insight> InsightAdded;

        /// <summary>
        /// An event that fires when an insight is updated.
        /// </summary>
        public event EventHandler<Insight> InsightUpdated;

        /// <summary>
        /// An event that fires when an insight is removed.
        /// </summary>
        public event EventHandler<Insight> InsightRemoved;

        /// <summary>
        /// Gets the collection of names that are reserved and cannot be used for custom insights.
        /// </summary>
        public static IEnumerable<string> ReservedInsightNames =>
        [
            "default", "admin", "system", "assets", "api", "workspace",
            "workspaces", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private InsightManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns an insight based on its id.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <returns>The insight.</returns>
        public Insight GetInsight(Guid insightId)
        {
            var query = new Query<Insight>()
                .Where(x => x.Id == insightId)
                .WithPaging(0, 1);

            return ModelHub.GetInsights(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns an insight based on its id.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <returns>The insight.</returns>
        public Insight GetInsight(InsightIdParameter insightId)
        {
            var guid = Guid.TryParse(insightId.Value, out Guid id) ? id : Guid.Empty;

            return GetInsight(guid);
        }

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
        public IEnumerable<Insight> GetInsights(IQuery<Insight> query)
        {
            return ModelHub.GetInsights(query);
        }

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
        public IEnumerable<Insight> GetInsights(IQuery<Insight> query, IQueryContext context)
        {
            return ModelHub.GetInsights(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds an insight to the manager.
        /// </summary>
        /// <param name="insight">The insight to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager Add(Insight insight)
        {
            ArgumentNullException.ThrowIfNull(insight);

            ModelHub.Add(insight);

            InsightAdded?.Invoke(this, insight);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.insight.created", insight);

            return this;
        }

        /// <summary>
        /// Updates an insight in the manager.
        /// </summary>
        /// <param name="insight">The insight to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager Update(Insight insight)
        {
            ArgumentNullException.ThrowIfNull(insight);

            ModelHub.Update(insight);

            InsightUpdated?.Invoke(this, insight);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.insight.updated", insight);

            return this;
        }

        /// <summary>
        /// Removes the specified insight from the manager.
        /// </summary>
        /// <remarks>This method removes the specified insight from the manager. If the insight does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="insightId">The insight id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager Remove(Guid insightId)
        {
            var insight = GetInsight(insightId);

            if (insight is not null)
            {
                ModelHub.Remove(insight);
                InsightRemoved?.Invoke(this, insight);

                CoreHub.AddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.insight.deleted", insight);
            }

            return this;
        }

        /// <summary>
        /// Applies a column-only layout change (add, rename, resize, recolor, reorder, delete) to a
        /// insight while preserving the widgets of the surviving columns.
        /// </summary>
        /// <param name="insightId">The id of the insight to update.</param>
        /// <param name="columns">
        /// The desired columns in their target order. Widgets on these instances are ignored. Must not
        /// be null.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager SetColumns(Guid insightId, IReadOnlyList<DashboardColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);

            ModelHub.SetDashboardColumns(insightId, columns);

            var insight = GetInsight(insightId);

            if (insight is not null)
            {
                InsightUpdated?.Invoke(this, insight);
            }

            return this;
        }

        /// <summary>
        /// Applies a full board update (a widget being added, deleted, reconfigured or moved) to a
        /// insight, rebuilding the widgets of every column from the desired state.
        /// </summary>
        /// <param name="insightId">The id of the insight to update.</param>
        /// <param name="columns">
        /// The desired columns, each carrying the widgets it should hold, in their target order. Must
        /// not be null.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager SetBoard(Guid insightId, IReadOnlyList<DashboardColumn> columns)
        {
            ArgumentNullException.ThrowIfNull(columns);

            ModelHub.SetDashboardBoard(insightId, columns);

            var insight = GetInsight(insightId);

            if (insight is not null)
            {
                InsightUpdated?.Invoke(this, insight);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
