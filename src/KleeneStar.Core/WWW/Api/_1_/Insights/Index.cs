using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Insights
{
    /// <summary>
    /// Provides CRUD operations for insight items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Insight>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Retrieves the response for the specified request using the configured retrieval logic.
        /// </summary>
        /// <param name="request">
        /// The request object containing the parameters for the retrieval operation. Must not be null.
        /// </param>
        /// <returns>
        /// An IResponse object that represents the result of the retrieval operation. The response 
        /// contains the data requested according to the parameters provided.
        /// </returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);

            var authorized = request?.GetParameter("mode")?.Value switch
            {
                "edit" => ContentAuthorization.MayUseInsight(id, request, typeof(WebPermissions.InsightUpdatePermission)),
                "delete" => ContentAuthorization.MayUseInsight(id, request, typeof(WebPermissions.InsightDeletePermission)),
                "clone" or "new" => ContentAuthorization.MayUseInsight(id, request, typeof(WebPermissions.InsightClonePermission)),
                _ => true
            };

            // a plain read is narrowed by the list itself (ContentVisibility)
            return authorized ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Creates an insight for a signed-in caller, or clones one they may clone.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return ContentAuthorization.MayUseInsight(ContentAuthorization.ReadId(request), request, typeof(WebPermissions.InsightClonePermission))
                ? base.Create(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Changes an insight, once the caller may.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return ContentAuthorization.MayUseInsight(ContentAuthorization.ReadId(request), request, typeof(WebPermissions.InsightUpdatePermission))
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Deletes an insight, once the caller may.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return ContentAuthorization.MayUseInsight(ContentAuthorization.ReadId(request), request, typeof(WebPermissions.InsightDeletePermission))
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
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
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
        /// A collection representing the filtered set of index items. 
        /// The collection may be empty if no items match the query.
        /// </returns>
        protected override IEnumerable<Model.Entities.Insight> Retrieve(IQuery<Model.Entities.Insight> query, IQueryContext context, IRequest request)
        {
            return CoreHub.InsightManager.GetInsights(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(query), context);
        }

        /// <summary>
        /// Retrieves the data required to create a new insight entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new insight for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            // the base answer, not this method again: the override used to call itself and
            // overflowed the stack on the first create-mode load
            return base.RetrieveForCreate(request);
        }

        /// <summary>
        /// Retrieves a result object containing default values and metadata for 
        /// cloning an insight.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">The request.</param>
        /// <returns>
        /// A result instance representing the data and metadata required
        /// to initialize a new insight for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Insight> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.InsightManager.GetInsights(query, context)
                .FirstOrDefault();

            var newItem = new Model.Entities.Insight()
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                Query = data.Query ?? string.Empty,
                State = InsightState.Active
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves an insight identified by the specified id for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the insight associated with the specified id.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Insight> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.InsightManager.GetInsights(query, context)
                .FirstOrDefault();

            // the query prompt fills its field from the loaded value; a missing one is an empty
            // query, which is what the field shows anyway
            if (data is not null)
            {
                data.Query ??= string.Empty;
            }

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the insight entity identified by the specified ID in preparation for deletion.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for 
        /// the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the insight entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Insight> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.InsightManager.GetInsights(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Name);
        }

        /// <summary>
        /// Validate the data for create or update operations. When creating, existingItem will 
        /// be null and proposedItem contains the values to create. When updating, existingItem 
        /// is the currently persisted entity and proposedItem contains the incoming values to 
        /// validate.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item (null for create).
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing updated fields.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        /// <returns>
        /// An IRestApiValidationResult indicating validation success or errors.
        /// </returns>
        protected override IRestApiValidationResult Validate(Model.Entities.Insight existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var (query, sent) = payload.TryRead(nameof(Model.Entities.Insight.Query));

            // the query is what every tab of the insight shows; one that does not compile would
            // leave every tab empty, so it is refused when it is written, with the parser's reason
            if (sent && !WqlFilter.TryValidate<Model.Entities.Object>(query, out var error))
            {
                result.Add
                (
                    string.Format(Translate(request, "kleenestar.core:insight.query.validation.invalid"), Translate(request, error)),
                    nameof(Model.Entities.Insight.Query)
                );
            }

            return result;
        }

        /// <summary>
        /// Stores a blank query as none, so "everything the reader may see" has one spelling.
        /// </summary>
        /// <param name="query">The submitted query.</param>
        /// <returns>The trimmed query, or <see langword="null"/>.</returns>
        private static string NormalizeQuery(string query)
        {
            return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        }

        /// <summary>
        /// Translates a key in the language of the request.
        /// </summary>
        /// <param name="request">The request carrying the culture, or null.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The translated text.</returns>
        private static string Translate(IRequest request, string key)
        {
            return request is null ? I18N.Translate(key) : I18N.Translate(request, key);
        }

        /// <summary>
        /// Persists the newly created resource.
        /// Override this method in derived classes to implement the actual
        /// persistence logic and return a result describing the creation.
        /// </summary>
        /// <param name="fieldMap">
        /// The dynamic payload containing the fields required to create the resource.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context for the creation process.
        /// </param>
        /// <param name="newItem">
        /// When the method returns, contains the newly created index item,
        /// or the default value if creation was not successful.
        /// </param>
        /// <returns>
        /// A result object containing information about the create operation,
        /// including the created resource.
        /// </returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Insight newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Insight(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = InsightState.Active
            };

            fieldMap.BindTo(newItem);

            newItem.Query = NormalizeQuery(newItem.Query);

            CoreHub.InsightManager.Add(newItem);

            // a new insight opens on its objects, with the reports beside them; everything else
            // is a tab the user adds
            CoreHub.InsightManager.AddDefaultViews(newItem.Id, key => Translate(request, key));

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new insight instance by cloning data from the specified form fields and 
        /// adds it to the insight manager.
        /// </summary>
        /// <param name="existingItem">
        /// The existing insight item to use as a reference for the clone operation. This parameter 
        /// is not modified.
        /// </param>
        /// <param name="fieldMap">
        /// The form data containing field values to bind to the new insight instance. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current request context for the operation. Provides additional information or 
        /// services required during cloning.
        /// </param>
        /// <param name="newItem">
        /// When this method returns, contains the newly created insight instance populated 
        /// with the provided form data.
        /// </param>
        /// <returns>
        /// A result object indicating the outcome of the create operation.
        /// </returns>
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Insight existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Insight newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Insight(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = InsightState.Active
            };

            fieldMap.BindTo(newItem);

            newItem.Query = NormalizeQuery(newItem.Query);

            CoreHub.InsightManager.Add(newItem);

            // a clone shows what its original shows: the same tabs and a copy of its dashboard
            CoreHub.InsightManager.CopyViews(existingItem.Id, newItem.Id);
            CoreHub.InsightManager.AddDefaultViews(newItem.Id, key => Translate(request, key));

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates the data record.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item.
        /// </param>
        /// <param name="payload">
        /// The dynamic payload containing updated fields.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context.
        /// </param>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Insight existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // the type is a record of what the insight was created as; no payload changes it
            var type = existingItem.Type;

            var res = base.Update(existingItem, payload, request);

            existingItem.Type = type;
            existingItem.Query = NormalizeQuery(existingItem.Query);

            CoreHub.InsightManager.Update(existingItem);

            return res;
        }

        /// <summary>
        /// Deletes the specified resource.
        /// </summary>
        /// <param name="existingItem">
        /// The currently persisted item that is to be deleted.
        /// </param>
        /// <param name="request">
        /// The HTTP request providing additional context for the delete operation.
        /// </param>
        /// <returns>
        /// A result object containing information about the delete operation.
        /// </returns>
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Insight existingItem, IRequest request)
        {
            CoreHub.InsightManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
