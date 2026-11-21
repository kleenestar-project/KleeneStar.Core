using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the maintenance notice of the installation: the instruction
    /// text that is shown to every user as a toast for as long as it is active.
    /// </summary>
    /// <remarks>
    /// The notice is a singleton, so the manager exposes it as a single record rather than as a
    /// collection. The query-based accessors exist for the REST endpoint, which addresses the
    /// record the same way it addresses any other entity.
    /// </remarks>
    public interface IMaintenanceManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when the maintenance notice is updated.
        /// </summary>
        event EventHandler<Maintenance> MaintenanceUpdated;

        /// <summary>
        /// Returns the maintenance notice of the installation.
        /// </summary>
        /// <returns>
        /// The maintenance notice. Never null; a disabled notice is returned when the record has
        /// not been stored yet, so callers do not have to distinguish the two.
        /// </returns>
        Maintenance GetMaintenance();

        /// <summary>
        /// Retrieves the maintenance notices that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned notices. Must not be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of maintenance notices that match the given criteria. If none
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Maintenance> GetMaintenances(IQuery<Maintenance> query);

        /// <summary>
        /// Retrieves the maintenance notices that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the returned notices. Must not be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of maintenance notices that match the given criteria. If none
        /// match, the collection will be empty.
        /// </returns>
        IEnumerable<Maintenance> GetMaintenances(IQuery<Maintenance> query, IQueryContext context);

        /// <summary>
        /// Determines whether an instruction text is currently to be shown to the users.
        /// </summary>
        /// <returns>
        /// True when the notice is enabled and carries a text; otherwise false.
        /// </returns>
        bool IsNoticeVisible();

        /// <summary>
        /// Updates the maintenance notice of the installation.
        /// </summary>
        /// <param name="maintenanceEntity">The maintenance notice to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IMaintenanceManager Update(Maintenance maintenanceEntity);
    }
}
