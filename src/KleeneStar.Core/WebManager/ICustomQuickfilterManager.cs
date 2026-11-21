using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the quickfilters the users defined themselves: named WQL
    /// expressions that appear as chips in the quickfilter bar of one view.
    /// </summary>
    public interface ICustomQuickfilterManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a quickfilter is added.
        /// </summary>
        event EventHandler<CustomQuickfilter> CustomQuickfilterAdded;

        /// <summary>
        /// An event that fires when a quickfilter is updated.
        /// </summary>
        event EventHandler<CustomQuickfilter> CustomQuickfilterUpdated;

        /// <summary>
        /// An event that fires when a quickfilter is removed.
        /// </summary>
        event EventHandler<CustomQuickfilter> CustomQuickfilterRemoved;

        /// <summary>
        /// Returns a quickfilter based on its id.
        /// </summary>
        /// <param name="quickfilterId">The id of the quickfilter.</param>
        /// <returns>The quickfilter, or null when no such filter is stored.</returns>
        CustomQuickfilter GetCustomQuickfilter(Guid quickfilterId);

        /// <summary>
        /// Retrieves the quickfilters that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned quickfilters. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of quickfilters that match the given criteria.
        /// </returns>
        IEnumerable<CustomQuickfilter> GetCustomQuickfilters(IQuery<CustomQuickfilter> query);

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
        IEnumerable<CustomQuickfilter> GetCustomQuickfilters(IQuery<CustomQuickfilter> query, IQueryContext context);

        /// <summary>
        /// Returns the quickfilters offered in the bar of a view, in the order they are shown.
        /// </summary>
        /// <remarks>
        /// A filter is offered to the identity that created it, and to everyone once it is shared.
        /// The ordering is applied after materialization so filters sharing an ordinal keep a
        /// stable, name-based order rather than an arbitrary storage order.
        /// </remarks>
        /// <param name="viewKey">The view whose bar is being filled.</param>
        /// <param name="contextKey">
        /// The context that narrows the view, or null for a view that exists only once.
        /// </param>
        /// <param name="identityId">The identity the bar is rendered for.</param>
        /// <returns>The quickfilters to offer, ordered by their ordinal and then by name.</returns>
        IEnumerable<CustomQuickfilter> GetVisibleCustomQuickfilters(string viewKey, string contextKey, Guid identityId);

        /// <summary>
        /// Adds a quickfilter to the manager.
        /// </summary>
        /// <param name="quickfilterEntity">The quickfilter to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ICustomQuickfilterManager Add(CustomQuickfilter quickfilterEntity);

        /// <summary>
        /// Updates a quickfilter of the manager.
        /// </summary>
        /// <param name="quickfilterEntity">The quickfilter to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ICustomQuickfilterManager Update(CustomQuickfilter quickfilterEntity);

        /// <summary>
        /// Removes the specified quickfilter from the manager.
        /// </summary>
        /// <remarks>
        /// If the quickfilter does not exist in the manager, no action is taken.
        /// </remarks>
        /// <param name="quickfilterId">The quickfilter id to be removed.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        ICustomQuickfilterManager Remove(Guid quickfilterId);
    }
}
