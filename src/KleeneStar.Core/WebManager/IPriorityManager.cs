using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing priorities, including adding, retrieving, and removing, as well as
    /// handling priority-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing priorities and events for tracking changes 
    /// to the priority collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public interface IPriorityManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an priority is added.
        /// </summary>
        event EventHandler<Priority> PriorityAdded;

        /// <summary>
        /// An event that fires when an priority is udpated.
        /// </summary>
        event EventHandler<Priority> PriorityUpdated;

        /// <summary>
        /// An event that fires when an priority is removed.
        /// </summary>
        event EventHandler<Priority> PriorityRemoved;

        /// <summary>
        /// Returns a priority based on its id.
        /// </summary>
        /// <param name="priorityId">The id of the priority.</param>
        /// <returns>The priority.</returns>
        Priority GetPriority(Guid priorityId);

        /// <summary>
        /// Returns a priority based on its id.
        /// </summary>
        /// <param name="fieldId">The id of the priority.</param>
        /// <returns>The priority.</returns>
        Priority GetPriority(PriorityIdParameter fieldId);

        /// <summary>
        /// Retrieves a collection of priorities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// An enumerable collection of priorities that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Priority> GetPriorities(ClassIdParameter classId);

        /// <summary>
        /// Retrieves a collection of priorities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned priorities. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of priorities that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Priority> GetPriorities(IQuery<Priority> query);

        /// <summary>
        /// Retrieves a collection of priorities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned priorities. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of priorities that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Priority> GetPriorities(IQuery<Priority> query, IQueryContext context);

        /// <summary>
        /// Adds a priority to the manager.
        /// </summary>
        /// <param name="priorityEntity">The priority to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IPriorityManager Add(Priority priorityEntity);

        /// <summary>
        /// Update a priority to the manager.
        /// </summary>
        /// <param name="priorityEntity">The priority to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IPriorityManager Update(Priority priorityEntity);

        /// <summary>
        /// Applies a new display order to a set of priorities. The position of each id
        /// in <paramref name="orderedIds"/> becomes the persisted Order value (0-based).
        /// </summary>
        /// <param name="orderedIds">The priority ids in the desired display order.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IPriorityManager Reorder(IReadOnlyList<Guid> orderedIds);

        /// <summary>
        /// Moves the specified priority one position towards the start or the end of the order of
        /// its class.
        /// </summary>
        /// <param name="priorityId">The id of the priority to move.</param>
        /// <param name="up">
        /// <c>true</c> to move the priority towards the start; otherwise towards the end.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IPriorityManager Move(Guid priorityId, bool up);

        /// <summary>
        /// Removes the specified priority from the manager.
        /// </summary>
        /// <remarks>This method removes the specified priority from the manager. If the field does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="priorityId">The priority id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IPriorityManager Remove(Guid priorityId);
    }
}
