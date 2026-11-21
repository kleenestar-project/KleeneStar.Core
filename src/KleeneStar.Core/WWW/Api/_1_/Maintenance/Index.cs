using WebExpress.WebCore.WebRestApi;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Maintenance
{
    /// <summary>
    /// Serves the maintenance notice of the installation to the settings form and takes its updates.
    /// </summary>
    /// <remarks>
    /// The notice is a singleton, so only the retrieve and update halves of the CRUD contract are
    /// meaningful here. Creating or deleting is left to the inherited implementations, which the
    /// settings form never calls because it addresses the fixed record directly.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Maintenance>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers a read; the reads that prepare a change (the new, edit, clone and delete
        /// dialogs) only for an administrator.
        /// </summary>
        /// <remarks>
        /// The notice is shown to every visitor, signed in or not, so a write open to anybody is a defacement open to anybody. The administrators are those of the accounts (<see cref="AccountAuthorization"/>),
        /// the only installation-wide administration there is.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var preparesChange = request?.GetParameter("mode")?.Value is "edit" or "delete" or "clone" or "new";

            return !preparesChange || AccountAuthorization.IsAdministrator(request)
                ? base.Retrieve(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Creates or clones an entry of the maintenance notice, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Create(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Changes an entry of the maintenance notice, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Removes an entry of the maintenance notice, once the caller is an administrator.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The HTTP response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return AccountAuthorization.IsAdministrator(request)
                ? base.Delete(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>
        /// An IQueryContext instance that can be used to execute queries.
        /// </returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the maintenance notices that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select the notices. Cannot
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// A collection representing the filtered set of notices. The collection may be empty if
        /// none match the query.
        /// </returns>
        protected override IEnumerable<Model.Entities.Maintenance> Retrieve(IQuery<Model.Entities.Maintenance> query, IQueryContext context, IRequest request)
        {
            return CoreHub.MaintenanceManager.GetMaintenances(query, context);
        }

        /// <summary>
        /// Retrieves the maintenance notice in preparation for an update.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to select the notice. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the maintenance notice.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Maintenance> query, IRequest request)
        {
            // the notice is the singleton the manager already holds, so the query is not applied
            // here and a fresh installation yields the disabled default rather than nothing
            return RetrieveForUpdate(request, CoreHub.MaintenanceManager.GetMaintenance());
        }

        /// <summary>
        /// Persists the edited maintenance notice.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted notice.
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing the edited notice.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        /// <returns>
        /// A result object containing information about the update operation.
        /// </returns>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Maintenance existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            CoreHub.MaintenanceManager.Update(existingItem);

            return res;
        }
    }
}
