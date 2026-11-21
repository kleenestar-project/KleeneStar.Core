using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Groups
{
    /// <summary>
    /// Provides CRUD operations for group items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Group>
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
        /// Groups carry permissions and security levels, so changing them changes who may do what. The administrators are those of the accounts (<see cref="AccountAuthorization"/>),
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
        /// Creates or clones an entry of the groups, once the caller is an administrator.
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
        /// Changes an entry of the groups, once the caller is an administrator.
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
        /// Removes an entry of the groups, once the caller is an administrator.
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
        /// Creates a new query context.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves groups matching the query.
        /// </summary>
        protected override IEnumerable<Model.Entities.Group> Retrieve(IQuery<Model.Entities.Group> query, IQueryContext context, IRequest request)
        {
            return CoreHub.GroupManager.GetGroups(query, context);
        }

        /// <summary>
        /// Retrieves data for creating a new group.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            return base.RetrieveForCreate(request);
        }

        /// <summary>
        /// Retrieves data for cloning a group.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Group> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.GroupManager.GetGroups(query, context)
                .FirstOrDefault();

            var newItem = new Model.Entities.Group()
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                State = GroupState.Active
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves data for updating a group.
        /// </summary>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Group> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.GroupManager.GetGroups(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves data for deleting a group.
        /// </summary>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Group> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.GroupManager.GetGroups(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Id.ToString());
        }

        /// <summary>
        /// Validates group data.
        /// </summary>
        protected override IRestApiValidationResult Validate(Model.Entities.Group existingItem, RestApiCrudFormData payload, IRequest request)
        {
            return base.Validate(existingItem, payload, request);
        }

        /// <summary>
        /// Creates a new group.
        /// </summary>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Group newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Group(id)
            {
                State = GroupState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.GroupManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Clones an existing group.
        /// </summary>
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Group existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Group newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Group(id)
            {
                State = GroupState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.GroupManager.Add(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates an existing group.
        /// </summary>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Group existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            CoreHub.GroupManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes a group.
        /// </summary>
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Group existingItem, IRequest request)
        {
            CoreHub.GroupManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
