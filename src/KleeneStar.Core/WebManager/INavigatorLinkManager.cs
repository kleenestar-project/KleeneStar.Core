using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the additional links shown in the app navigator, including
    /// adding, retrieving, and removing, as well as handling link-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing navigator links and events for tracking changes
    /// to the link collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public interface INavigatorLinkManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a navigator link is added.
        /// </summary>
        event EventHandler<NavigatorLink> NavigatorLinkAdded;

        /// <summary>
        /// An event that fires when a navigator link is updated.
        /// </summary>
        event EventHandler<NavigatorLink> NavigatorLinkUpdated;

        /// <summary>
        /// An event that fires when a navigator link is removed.
        /// </summary>
        event EventHandler<NavigatorLink> NavigatorLinkRemoved;

        /// <summary>
        /// Returns a navigator link based on its id.
        /// </summary>
        /// <param name="navigatorLinkId">The id of the navigator link.</param>
        /// <returns>The navigator link.</returns>
        NavigatorLink GetNavigatorLink(Guid navigatorLinkId);

        /// <summary>
        /// Retrieves a collection of navigator links that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned navigator links. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of navigator links that match the given predicate. If none
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<NavigatorLink> GetNavigatorLinks(IQuery<NavigatorLink> query);

        /// <summary>
        /// Retrieves a collection of navigator links that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned navigator links. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of navigator links that match the given predicate. If none
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<NavigatorLink> GetNavigatorLinks(IQuery<NavigatorLink> query, IQueryContext context);

        /// <summary>
        /// Returns the active navigator links in the order in which they are shown in the app navigator.
        /// </summary>
        /// <returns>
        /// The active navigator links, ordered by their ordinal and then by name.
        /// </returns>
        IEnumerable<NavigatorLink> GetVisibleNavigatorLinks();

        /// <summary>
        /// Returns all navigator links in the order in which they are listed and shown.
        /// </summary>
        /// <returns>The navigator links, ordered by their ordinal and then by name.</returns>
        IEnumerable<NavigatorLink> GetOrderedNavigatorLinks();

        /// <summary>
        /// Applies the specified order to the navigator links.
        /// </summary>
        /// <param name="orderedIds">The link ids in the desired order.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        INavigatorLinkManager Reorder(IEnumerable<Guid> orderedIds);

        /// <summary>
        /// Moves the specified navigator link one position towards the start or the end.
        /// </summary>
        /// <param name="navigatorLinkId">The id of the link to move.</param>
        /// <param name="up">
        /// <c>true</c> to move the link towards the start; otherwise towards the end.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        INavigatorLinkManager Move(Guid navigatorLinkId, bool up);

        /// <summary>
        /// Adds a navigator link to the manager.
        /// </summary>
        /// <param name="navigatorLinkEntity">The navigator link to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        INavigatorLinkManager Add(NavigatorLink navigatorLinkEntity);

        /// <summary>
        /// Updates a navigator link of the manager.
        /// </summary>
        /// <param name="navigatorLinkEntity">The navigator link to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        INavigatorLinkManager Update(NavigatorLink navigatorLinkEntity);

        /// <summary>
        /// Removes the specified navigator link from the manager.
        /// </summary>
        /// <param name="navigatorLinkId">The navigator link id to be removed.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        INavigatorLinkManager Remove(Guid navigatorLinkId);
    }
}
