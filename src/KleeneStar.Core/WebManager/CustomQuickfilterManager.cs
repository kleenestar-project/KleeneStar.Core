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
    /// Manages the quickfilters the users defined themselves, including adding, retrieving and
    /// removing, as well as handling filter-related events.
    /// </summary>
    public sealed class CustomQuickfilterManager : ICustomQuickfilterManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a quickfilter is added.
        /// </summary>
        public event EventHandler<CustomQuickfilter> CustomQuickfilterAdded;

        /// <summary>
        /// An event that fires when a quickfilter is updated.
        /// </summary>
        public event EventHandler<CustomQuickfilter> CustomQuickfilterUpdated;

        /// <summary>
        /// An event that fires when a quickfilter is removed.
        /// </summary>
        public event EventHandler<CustomQuickfilter> CustomQuickfilterRemoved;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private CustomQuickfilterManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a quickfilter based on its id.
        /// </summary>
        /// <param name="quickfilterId">The id of the quickfilter.</param>
        /// <returns>The quickfilter, or null when no such filter is stored.</returns>
        public CustomQuickfilter GetCustomQuickfilter(Guid quickfilterId)
        {
            var query = new Query<CustomQuickfilter>()
                .WhereEquals(x => x.Id, quickfilterId)
                .WithPaging(0, 1);

            return ModelHub.GetCustomQuickfilters(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Retrieves the quickfilters that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned quickfilters. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of quickfilters that match the given criteria.
        /// </returns>
        public IEnumerable<CustomQuickfilter> GetCustomQuickfilters(IQuery<CustomQuickfilter> query)
        {
            return ModelHub.GetCustomQuickfilters(query);
        }

        /// <summary>
        /// Retrieves the quickfilters that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned quickfilters. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of quickfilters that match the given criteria.
        /// </returns>
        public IEnumerable<CustomQuickfilter> GetCustomQuickfilters(IQuery<CustomQuickfilter> query, IQueryContext context)
        {
            return ModelHub.GetCustomQuickfilters(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Returns the quickfilters offered in the bar of a view, in the order they are shown.
        /// </summary>
        /// <param name="viewKey">The view whose bar is being filled.</param>
        /// <param name="contextKey">
        /// The context that narrows the view, or null for a view that exists only once.
        /// </param>
        /// <param name="identityId">The identity the bar is rendered for.</param>
        /// <returns>The quickfilters to offer, ordered by their ordinal and then by name.</returns>
        public IEnumerable<CustomQuickfilter> GetVisibleCustomQuickfilters(string viewKey, string contextKey, Guid identityId)
        {
            if (string.IsNullOrWhiteSpace(viewKey))
            {
                return [];
            }

            var query = new Query<CustomQuickfilter>()
                .WhereEquals(x => x.ViewKey, viewKey);

            // the context is compared after materialization rather than in the query, because a
            // global view stores null here and an equality filter would not match it
            return [.. ModelHub.GetCustomQuickfilters(query)
                .Where(x => string.Equals(x.ContextKey ?? string.Empty, contextKey ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.Shared || x.OwnerId == identityId)
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Name)];
        }

        /// <summary>
        /// Adds a quickfilter to the manager.
        /// </summary>
        /// <param name="quickfilterEntity">The quickfilter to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ICustomQuickfilterManager Add(CustomQuickfilter quickfilterEntity)
        {
            ArgumentNullException.ThrowIfNull(quickfilterEntity);

            ModelHub.Add(quickfilterEntity);

            CustomQuickfilterAdded?.Invoke(this, quickfilterEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.quickfilter.created", quickfilterEntity);

            return this;
        }

        /// <summary>
        /// Updates a quickfilter of the manager.
        /// </summary>
        /// <param name="quickfilterEntity">The quickfilter to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ICustomQuickfilterManager Update(CustomQuickfilter quickfilterEntity)
        {
            ArgumentNullException.ThrowIfNull(quickfilterEntity);

            quickfilterEntity.Updated = DateTime.UtcNow;

            ModelHub.Update(quickfilterEntity);

            CustomQuickfilterUpdated?.Invoke(this, quickfilterEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.quickfilter.updated", quickfilterEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified quickfilter from the manager.
        /// </summary>
        /// <param name="quickfilterId">The quickfilter id to be removed.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public ICustomQuickfilterManager Remove(Guid quickfilterId)
        {
            var quickfilterEntry = GetCustomQuickfilter(quickfilterId);

            if (quickfilterEntry is not null)
            {
                ModelHub.Remove(quickfilterEntry);
                CustomQuickfilterRemoved?.Invoke(this, quickfilterEntry);
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
