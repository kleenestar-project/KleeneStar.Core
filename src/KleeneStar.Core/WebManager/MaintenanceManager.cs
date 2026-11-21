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
    /// Manages the maintenance notice of the installation: the instruction text that is shown to
    /// every user as a toast for as long as it is active.
    /// </summary>
    /// <remarks>
    /// The notice is read on every page render, both by the condition that decides whether the
    /// toast appears and by the toast itself. It is therefore held in memory and only re-read after
    /// an update, so an announcement nobody is currently making does not cost a query per request.
    /// </remarks>
    public sealed class MaintenanceManager : IMaintenanceManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly object _sync = new();

        private Maintenance _cached;

        /// <summary>
        /// An event that fires when the maintenance notice is updated.
        /// </summary>
        public event EventHandler<Maintenance> MaintenanceUpdated;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private MaintenanceManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the maintenance notice of the installation.
        /// </summary>
        /// <remarks>
        /// A notice that has not been stored yet is reported as a disabled one rather than as
        /// nothing, so neither the toast nor the settings page has to treat the first start of a
        /// fresh installation as a special case.
        /// </remarks>
        /// <returns>The maintenance notice. Never null.</returns>
        public Maintenance GetMaintenance()
        {
            var cached = _cached;

            if (cached is not null)
            {
                return cached;
            }

            lock (_sync)
            {
                var query = new Query<Maintenance>()
                    .WhereEquals(x => x.Id, Maintenance.SingletonId)
                    .WithPaging(0, 1);

                return _cached ??= ModelHub.GetMaintenances(query).FirstOrDefault()
                    ?? new Maintenance() { Enabled = false };
            }
        }

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
        public IEnumerable<Maintenance> GetMaintenances(IQuery<Maintenance> query)
        {
            return ModelHub.GetMaintenances(query);
        }

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
        public IEnumerable<Maintenance> GetMaintenances(IQuery<Maintenance> query, IQueryContext context)
        {
            return ModelHub.GetMaintenances(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Determines whether an instruction text is currently to be shown to the users.
        /// </summary>
        /// <remarks>
        /// An enabled notice without a text is treated as invisible, because an empty toast tells
        /// the user nothing while still taking up the top of every page. The text comes out of the
        /// prose editor, which never submits an empty string, so emptiness is asked of the
        /// document (<see cref="WebControl.ProseText.IsEmpty"/>), not of the string.
        /// </remarks>
        /// <returns>True when the notice is enabled and carries a text; otherwise false.</returns>
        public bool IsNoticeVisible()
        {
            var maintenance = GetMaintenance();

            return maintenance.Enabled && !WebControl.ProseText.IsEmpty(maintenance.Message);
        }

        /// <summary>
        /// Updates the maintenance notice of the installation.
        /// </summary>
        /// <param name="maintenanceEntity">The maintenance notice to update. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IMaintenanceManager Update(Maintenance maintenanceEntity)
        {
            ArgumentNullException.ThrowIfNull(maintenanceEntity);

            ModelHub.Update(maintenanceEntity);

            lock (_sync)
            {
                _cached = null;
            }

            MaintenanceUpdated?.Invoke(this, maintenanceEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.maintenance.updated", maintenanceEntity);

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
