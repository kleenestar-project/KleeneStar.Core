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
    /// Defines the contract for managing workflow states, including adding, retrieving, and removing, as well as
    /// handling workflow states-related events.
    /// </summary>
    /// <remarks>
    /// The interface provides methods for managing workflow states and events for tracking changes
    /// to the workflow state collection. Implementations of this interface should ensure thread
    /// safety if used in a multi-threaded environment.
    /// </remarks>
    public sealed class StatusManager : IStatusManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an workflow state is added.
        /// </summary>
        public event EventHandler<Status> StatusAdded;

        /// <summary>
        /// An event that fires when an workflow state is udpated.
        /// </summary>
        public event EventHandler<Status> StatusUpdated;

        /// <summary>
        /// An event that fires when an workflow state is removed.
        /// </summary>
        public event EventHandler<Status> StatusRemoved;

        /// <summary>
        /// Gets the collection of status names that are reserved and cannot be used for custom statuses.
        /// </summary>
        /// <remarks>
        /// The reserved names typically represent system-defined routes and are not available
        /// for user-defined or custom status creation.
        /// </remarks>
        public static IEnumerable<string> ReservedStateNames =>
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
        private StatusManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Retrieves a collection of status categories that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// The query used to filter and select status categories. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of status categories that satisfy the query conditions. The 
        /// collection is empty if no status categories match the query.
        /// </returns>
        public IEnumerable<StatusCategory> GetStatusCategories(IQuery<StatusCategory> query)
        {
            return ModelHub.GetStatusCategories(query);
        }

        /// <summary>
        /// Retrieves a collection of status categories that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// The query used to filter and select status categories. Cannot be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of status categories that satisfy the query conditions. The 
        /// collection is empty if no status categories match the query.
        /// </returns>
        public IEnumerable<StatusCategory> GetStatusCategories(IQuery<StatusCategory> query, IQueryContext context)
        {
            return ModelHub.GetStatusCategories(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Returns a workflow state based on its id.
        /// </summary>
        /// <param name="workflowId">The id of the workflow state.</param>
        /// <returns>The workflow state.</returns>
        public Status GetStatus(Guid workflowId)
        {
            return RequestReadCache.Get(nameof(GetStatus), workflowId, () =>
            {
                var query = new Query<Status>()
                    .Where(x => x.Id == workflowId)
                    .WithPaging(0, 1);

                return ModelHub.GetStatuses(query)
                    .FirstOrDefault();
            }, typeof(Status), typeof(StatusCategory));
        }

        /// <summary>
        /// Returns a workflow state based on its id.
        /// </summary>
        /// <param name="workflowId">The id of the workflow state.</param>
        /// <returns>The workflow state.</returns>
        public Status GetStatus(WorkflowStateIdParameter workflowId)
        {
            var guid = Guid.TryParse(workflowId.Value, out Guid id) ? id : Guid.Empty;

            return GetStatus(guid);
        }

        /// <summary>
        /// Retrieves a collection of workflow states that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="classId">The id of the class.</param>
        /// <returns>
        /// An enumerable collection of workflow states that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Status> GetStatuses(ClassIdParameter classId)
        {
            var guid = Guid.TryParse(classId.Value, out Guid id) ? id : Guid.Empty;

            return RequestReadCache.Get<IReadOnlyList<Status>>(nameof(GetStatuses), guid, () =>
            {
                var query = new Query<Status>()
                    .WhereEquals(x => x.ClassId, guid);

                return [.. ModelHub.GetStatuses(query)];
            }, typeof(Status), typeof(StatusCategory));
        }

        /// <summary>
        /// Retrieves a collection of workflow states that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workflow states. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workflow states that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Status> GetStatuses(IQuery<Status> query)
        {
            return ModelHub.GetStatuses(query);
        }

        /// <summary>
        /// Retrieves a collection of workflow states that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned workflow states. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of workflow states that match the given predicate. If no class 
        /// match, the collection will be empty.
        /// </returns>
        public IEnumerable<Status> GetStatuses(IQuery<Status> query, IQueryContext context)
        {
            return ModelHub.GetStatuses(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds a workflow state to the manager.
        /// </summary>
        /// <param name="stateEntity">The workflow state to add. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IStatusManager Add(Status stateEntity)
        {
            ArgumentNullException.ThrowIfNull(stateEntity);

            ModelHub.Add(stateEntity);

            StatusAdded?.Invoke(this, stateEntity);

            // create notification
            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.status.created", stateEntity);

            return this;
        }

        /// <summary>
        /// Update a workflow state to the manager.
        /// </summary>
        /// <param name="stateEntity">The workflow state to updated. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IStatusManager Update(Status stateEntity)
        {
            ArgumentNullException.ThrowIfNull(stateEntity);

            ModelHub.Update(stateEntity);

            StatusUpdated?.Invoke(this, stateEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.status.updated", stateEntity);

            return this;
        }

        /// <summary>
        /// Removes the specified workflow state from the manager.
        /// </summary>
        /// <remarks>This method removes the specified workflow state from the manager. If the workflow state does
        /// not exist in the manager, no action is taken.</remarks>
        /// <param name="stateId">The workflow state id to be removed. Must not be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IStatusManager Remove(Guid stateId)
        {
            var steteEntry = GetStatus(stateId);

            if (steteEntry is not null)
            {
                ModelHub.Remove(steteEntry);
                StatusRemoved?.Invoke(this, steteEntry);
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
