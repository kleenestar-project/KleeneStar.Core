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
        /// Returns the tabs of an insight in display order.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <returns>The tabs, ordered by position.</returns>
        public IReadOnlyList<InsightView> GetViews(Guid insightId)
        {
            var query = new Query<InsightView>()
                .WhereEquals(x => x.InsightId, insightId);

            return [.. ModelHub.GetInsightViews(query)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Created)];
        }

        /// <summary>
        /// Returns a tab by its id.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <returns>The tab, or <see langword="null"/> when there is none.</returns>
        public InsightView GetView(Guid viewId)
        {
            var query = new Query<InsightView>()
                .WhereEquals(x => x.Id, viewId);

            return ModelHub.GetInsightViews(query).FirstOrDefault();
        }

        /// <summary>
        /// Adds a tab to an insight, behind the tabs it has, under a name no other tab of the
        /// insight carries.
        /// </summary>
        /// <param name="view">The tab to add. Its insight must exist. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager AddView(InsightView view)
        {
            ArgumentNullException.ThrowIfNull(view);

            var insight = GetInsight(view.InsightId);

            if (insight is null)
            {
                return this;
            }

            var existing = GetViews(view.InsightId);

            view.Name = UniqueName(existing, string.IsNullOrWhiteSpace(view.Name) ? view.ViewType : view.Name.Trim());
            view.Order = existing.Count == 0 ? 0 : existing.Max(x => x.Order) + 1;
            view.Created = view.Created == default ? DateTime.UtcNow : view.Created;
            view.Updated = DateTime.UtcNow;

            ModelHub.Add(view);

            InsightUpdated?.Invoke(this, insight);

            return this;
        }

        /// <summary>
        /// Removes a tab.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <returns><see langword="true"/> when a tab was removed.</returns>
        public bool RemoveView(Guid viewId)
        {
            var view = GetView(viewId);

            if (view is null)
            {
                return false;
            }

            ModelHub.Remove(view);

            var insight = GetInsight(view.InsightId);

            if (insight is not null)
            {
                InsightUpdated?.Invoke(this, insight);
            }

            return true;
        }

        /// <summary>
        /// Puts the tabs of an insight into the given order.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <param name="order">The tab ids in their new order. Cannot be null.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        public bool ReorderViews(Guid insightId, IReadOnlyList<Guid> order)
        {
            ArgumentNullException.ThrowIfNull(order);

            return ModelHub.SetInsightViewOrder(insightId, order);
        }

        /// <summary>
        /// Gives an insight the tabs a new insight starts with - the objects and the reports -
        /// unless it has tabs already.
        /// </summary>
        /// <param name="insightId">The id of the insight.</param>
        /// <param name="nameOf">Resolves the name of a tab from its view type's label key.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager AddDefaultViews(Guid insightId, Func<string, string> nameOf)
        {
            if (GetViews(insightId).Count > 0)
            {
                return this;
            }

            foreach (var key in new[] { WebInsight.InsightViewTypeCatalog.Default, InsightViewTypes.Reports })
            {
                var type = WebInsight.InsightViewTypeCatalog.Get(key);

                if (type is null)
                {
                    continue;
                }

                AddView(new InsightView
                {
                    InsightId = insightId,
                    ViewType = WebInsight.InsightViewTypeCatalog.Normalize(type.Key),
                    Name = nameOf?.Invoke(type.Label) ?? type.Key,
                    State = ObjectViewState.Active
                });
            }

            return this;
        }

        /// <summary>
        /// Copies the tabs and the dashboard of one insight onto another that has none.
        /// </summary>
        /// <param name="sourceId">The insight copied from.</param>
        /// <param name="targetId">The insight copied to.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IInsightManager CopyViews(Guid sourceId, Guid targetId)
        {
            var source = GetInsight(sourceId);

            if (source is null || GetInsight(targetId) is null || GetViews(targetId).Count > 0)
            {
                return this;
            }

            foreach (var view in GetViews(sourceId))
            {
                AddView(new InsightView
                {
                    InsightId = targetId,
                    Name = view.Name,
                    ViewType = view.ViewType,
                    Configuration = view.Configuration,
                    State = view.State
                });
            }

            // the dashboard is the insight's, so a copy of its dashboard tabs needs the board
            // they show; fresh ids keep the two boards apart from here on
            if (source.Columns is { Count: > 0 })
            {
                SetBoard(targetId,
                [
                    .. source.Columns
                        .OrderBy(x => x.Position)
                        .Select(x => new DashboardColumn(Guid.Empty)
                        {
                            Name = x.Name,
                            Size = x.Size,
                            Color = x.Color,
                            Widgets = [.. (x.Widgets ?? [])
                                .OrderBy(w => w.Position)
                                .Select(w => new Widget(Guid.Empty)
                                {
                                    Type = w.Type,
                                    Name = w.Name,
                                    Color = w.Color,
                                    Params = w.Params,
                                    Wql = w.Wql
                                })]
                        })
                ]);
            }

            return this;
        }

        /// <summary>
        /// Returns a name based on <paramref name="seed"/> that no tab of the list carries.
        /// </summary>
        /// <param name="existing">The tabs of the insight.</param>
        /// <param name="seed">The desired name.</param>
        /// <returns>The name, with a number appended when the seed is taken.</returns>
        private static string UniqueName(IEnumerable<InsightView> existing, string seed)
        {
            var taken = existing
                .Select(x => x.Name ?? string.Empty)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            seed = string.IsNullOrWhiteSpace(seed) ? "View" : seed;

            // the name column holds 64 characters; leave room for the suffix
            if (seed.Length > 56)
            {
                seed = seed[..56];
            }

            if (!taken.Contains(seed))
            {
                return seed;
            }

            for (var i = 2; i < 1000; i++)
            {
                var candidate = $"{seed} ({i})";

                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            return Guid.NewGuid().ToString("N");
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
