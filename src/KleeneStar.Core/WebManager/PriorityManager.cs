using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore;
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
    public sealed class PriorityManager : IPriorityManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an priority is added.
        /// </summary>
        public event EventHandler<Priority> PriorityAdded;

        /// <summary>
        /// An event that fires when an priority is udpated.
        /// </summary>
        public event EventHandler<Priority> PriorityUpdated;

        /// <summary>
        /// An event that fires when an priority is removed.
        /// </summary>
        public event EventHandler<Priority> PriorityRemoved;

        /// <summary>
        /// Returns the collection of priority names that are reserved and cannot be used for custom priorities.
        /// </summary>
        /// <remarks>
        /// The reserved names typically represent system-defined routes and are not available
        /// for user-defined or custom priority creation.
        /// </remarks>
        public static IEnumerable<string> ReservedPriorityNames =>
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
        private PriorityManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns a priority based on its id.
        /// </summary>
        /// <param name="priorityId">The id of the priority.</param>
        /// <returns>The priority.</returns>
        public Priority GetPriority(Guid priorityId)
        {
            var query = new Query<Priority>()
                .Where(x => x.Id == priorityId)
                .WithPaging(0, 1);

            return ModelHub.GetPriorities(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns a priority based on its id.
        /// </summary>
        /// <param name="fieldId">The id of the priority.</param>
        /// <returns>The priority.</returns>
        public Priority GetPriority(PriorityIdParameter fieldId)
        {
            var guid = Guid.TryParse(fieldId.Value, out Guid id) ? id : Guid.Empty;

            return GetPriority(guid);
        }

        /// <summary>
        /// Retrieves a collection of priorities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// An enumerable collection of priorities that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Priority> GetPriorities(ClassIdParameter classId)
        {
            var guid = Guid.TryParse(classId.Value, out Guid id) ? id : Guid.Empty;
            var query = new Query<Priority>()
                .WhereEquals(x => x.ClassId, guid);

            return ModelHub.GetPriorities(query);
        }

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
        public IEnumerable<Priority> GetPriorities(IQuery<Priority> query)
        {
            return ModelHub.GetPriorities(query);
        }

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
        public IEnumerable<Priority> GetPriorities(IQuery<Priority> query, IQueryContext context)
        {
            return ModelHub.GetPriorities(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a priority to the manager.
        /// </summary>
        /// <param name="priorityEntity">The priority to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IPriorityManager Add(Priority priorityEntity)
        {
            ArgumentNullException.ThrowIfNull(priorityEntity);

            ModelHub.Add(priorityEntity);

            PriorityAdded?.Invoke(this, priorityEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.priority.created", priorityEntity);

            return this;
        }

        /// <summary>
        /// Update a priority to the manager.
        /// </summary>
        /// <param name="priorityEntity">The priority to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IPriorityManager Update(Priority priorityEntity)
        {
            ArgumentNullException.ThrowIfNull(priorityEntity);

            ModelHub.Update(priorityEntity);

            PriorityUpdated?.Invoke(this, priorityEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.priority.updated", priorityEntity);

            return this;
        }

        /// <summary>
        /// Applies a new display order to a set of priorities. The position of each id
        /// in <paramref name="orderedIds"/> becomes the persisted Order value (0-based).
        /// </summary>
        /// <param name="orderedIds">The priority ids in the desired display order.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IPriorityManager Reorder(IReadOnlyList<Guid> orderedIds)
        {
            ArgumentNullException.ThrowIfNull(orderedIds);

            ModelHub.ReorderPriorities(orderedIds);

            // neither the table endpoint that receives a dragged order nor a move announces the
            // change, so without this the other clients keep showing the previous order until the
            // page is loaded again
            _ = DataChangedNotifier.NotifyAsync<Priority>(DataChangeOperation.Updated);

            // raise the per-entity updated events in the requested order. The reordered
            // priorities are reloaded with a single query (filtered in memory) rather than
            // one GetPriority(id) round-trip per id; skipped entirely when nobody listens.
            var handler = PriorityUpdated;
            if (handler is not null && orderedIds.Count > 0)
            {
                var ids = orderedIds.ToHashSet();
                var byId = GetPriorities(new Query<Priority>())
                    .Where(p => ids.Contains(p.Id))
                    .ToDictionary(p => p.Id);

                foreach (var id in orderedIds)
                {
                    if (byId.TryGetValue(id, out var priority))
                    {
                        handler(this, priority);
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Moves the specified priority one position towards the start or the end of the order of
        /// its class.
        /// </summary>
        /// <remarks>
        /// The move is confined to the priorities of the same class, because the order is a property
        /// of that class rather than a global one.
        /// </remarks>
        /// <param name="priorityId">The id of the priority to move.</param>
        /// <param name="up">
        /// <c>true</c> to move the priority towards the start; otherwise towards the end.
        /// </param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IPriorityManager Move(Guid priorityId, bool up)
        {
            var priority = GetPriority(priorityId);

            if (priority is null)
            {
                return this;
            }

            var ordered = GetPriorities(new Query<Priority>()
                .WhereEquals(x => x.ClassId, priority.ClassId))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToList();

            var index = ordered.FindIndex(x => x.Id == priorityId);
            var target = up ? index - 1 : index + 1;

            // already at the end it is asked to move towards: a repeated click must not wrap the
            // entry around to the other end
            if (index < 0 || target < 0 || target >= ordered.Count)
            {
                return this;
            }

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            return Reorder([.. ordered.Select(x => x.Id)]);
        }

        /// <summary>
        /// Removes the specified priority from the manager.
        /// </summary>
        /// <remarks>This method removes the specified priority from the manager. If the field does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="priorityId">The priority id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IPriorityManager Remove(Guid priorityId)
        {
            var priorityEntry = GetPriority(priorityId);

            if (priorityEntry is not null)
            {
                ModelHub.Remove(priorityEntry);
                PriorityRemoved?.Invoke(this, priorityEntry);
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
