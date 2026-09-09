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

namespace KleeneStar.Core.WWW.Api._1_.Templates
{
    /// <summary>
    /// Provides CRUD operations for template items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Template>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
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
        protected override IEnumerable<Model.Entities.Template> Retrieve(IQuery<Model.Entities.Template> query, IQueryContext context, IRequest request)
        {
            return CoreHub.TemplateManager.GetTemplates(query, context);
        }

        /// <summary>
        /// Retrieves a result object containing default values and metadata for
        /// cloning a item.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">The request.</param>
        /// <returns>
        /// A result instance representing the data and metadata required
        /// to initialize a new item for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Template> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TemplateManager.GetTemplates(query, context)
                .FirstOrDefault();

            var newItem = new Model.Entities.Template()
            {
                Name = data?.Name + " (Copy)",
                Description = data?.Description,
                Category = data?.Category,
                Icon = data?.Icon,
                State = TemplateState.Active,
                Presets = data?.Presets,
                ParentId = data?.ParentId,
                Order = data?.Order ?? 0,
                ClassId = data?.ClassId ?? Guid.Empty
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a template identified by the specified id for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the template associated with the specified id.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Template> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TemplateManager.GetTemplates(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the template entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the template entity and related information required
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Template> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TemplateManager.GetTemplates(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Id.ToString());
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
        protected override IRestApiValidationResult Validate(Model.Entities.Template existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var errors = new List<RestApiError>();

            // a template instantiates exactly one class, so a create without a resolvable class
            // would otherwise reach the database and fail there on the foreign key. Clone and
            // update take the class from the existing record and therefore carry none.
            if (existingItem is null && !TryResolveClass(payload, out _))
            {
                errors.Add(new RestApiError
                (
                    I18N.Translate(request, "kleenestar.core:template.class.placeholder"),
                    field: nameof(Model.Entities.Template.ClassId)
                ));
            }

            // the parent reference is rejected here rather than at the database, which knows only
            // that the row exists and nothing about workspaces or cycles
            errors.Add(ValidateHierarchy(existingItem, payload, nameof(Model.Entities.Template.ParentId), request));

            errors.RemoveAll(x => x is null);

            if (errors.Count == 0)
            {
                return result;
            }

            return new RestApiValidationResult()
                .Add([.. result?.Errors ?? []])
                .Add([.. errors]);
        }

        /// <summary>
        /// Persists the newly created resource.
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
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Template newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Template(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = TemplateState.Active
            };

            fieldMap.BindTo(newItem);

            // the class is taken from the payload only when it names one that exists, so an
            // unknown id becomes an empty reference the validation reports rather than a
            // dangling one the insert fails on
            TryResolveClass(fieldMap, out var classId);
            newItem.ClassId = classId;

            CoreHub.TemplateManager.AddTemplate(newItem);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Creates a new instance by cloning data from the specified form fields and
        /// adds it to the template manager.
        /// </summary>
        /// <param name="existingItem">
        /// The existing item to use as a reference for the clone operation. This parameter
        /// is not modified.
        /// </param>
        /// <param name="fieldMap">
        /// The form data containing field values to bind to the new instance. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current request context for the operation. Provides additional information or
        /// services required during cloning.
        /// </param>
        /// <param name="newItem">
        /// When this method returns, contains the newly created instance populated
        /// with the provided form data.
        /// </param>
        /// <returns>
        /// A result object indicating the outcome of the create operation.
        /// </returns>
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Template existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Template newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Template(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = TemplateState.Active,
                Presets = existingItem?.Presets
            };

            fieldMap.BindTo(newItem);

            // the class a template instantiates is fixed at creation time and therefore not
            // part of the clone form — carrying it over from the source keeps the copy bound
            // to the same class and its presets meaningful
            newItem.ClassId = existingItem?.ClassId
                ?? (TryResolveClass(fieldMap, out var classId) ? classId : Guid.Empty);

            CoreHub.TemplateManager.AddTemplate(newItem);

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
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Template existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // the avatar dialog posts its picture inline as a data url, which the binder would
            // hand to RestValueConverterImageIcon and collapse to the URI "http:///" - while the
            // request still answers 200. So it is taken out of the payload before the binder sees
            // it, decoded, and written to the icons directory. See RestApiCrudFormDataAvatarExtensions.
            var avatarSent = payload.Detach(nameof(Model.Entities.Template.Icon), out var avatar);

            var res = base.Update(existingItem, payload, request);

            if (avatarSent)
            {
                existingItem.Icon = RestApiCrudFormDataAvatarExtensions.Resolve
                (
                    existingItem.Id,
                    avatar,
                    existingItem.Icon,
                    () => CoreHub.GenerateIcon(existingItem.Id)
                );
            }

            CoreHub.TemplateManager.UpdateTemplate(existingItem);

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
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Template existingItem, IRequest request)
        {
            CoreHub.TemplateManager.RemoveTemplate(existingItem);

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// Validates the parent reference of the payload: the referenced template must exist,
        /// belong to the same workspace, and not close a cycle.
        /// </summary>
        /// <param name="existingItem">
        /// The template being edited, or null while one is being created — a template that does
        /// not exist yet cannot be part of a cycle, so only the workspace rule applies to it.
        /// </param>
        /// <param name="payload">The payload carrying the reference.</param>
        /// <param name="field">The name of the reference field on the entity.</param>
        /// <param name="request">The request, for localizing the message.</param>
        /// <returns>The validation error, or null when the reference is valid or absent.</returns>
        private static RestApiError ValidateHierarchy(Model.Entities.Template existingItem, RestApiCrudFormData payload, string field, IRequest request)
        {
            if (!payload.TryGetGuidReference(field, out var referenceId) || referenceId is null)
            {
                return null;
            }

            var reference = CoreHub.TemplateManager.GetTemplate(referenceId.Value);
            var workspaceId = existingItem?.Class?.WorkspaceId
                ?? (TryResolveClass(payload, out var classId)
                    ? CoreHub.ClassManager.GetClass(classId)?.WorkspaceId
                    : null);

            if (reference is null || (workspaceId is not null && reference.Class?.WorkspaceId != workspaceId))
            {
                return new RestApiError
                (
                    I18N.Translate(request, "kleenestar.core:template.hierarchy.workspace"),
                    field: field
                );
            }

            if (existingItem is not null &&
                CoreHub.TemplateManager.WouldFormCycle(existingItem.Id, referenceId.Value))
            {
                return new RestApiError
                (
                    I18N.Translate(request, "kleenestar.core:template.parent.cycle"),
                    field: field
                );
            }

            return null;
        }

        /// <summary>
        /// Resolves the class a payload binds a template to and verifies that it exists, so an
        /// unknown or missing reference is reported as a validation error instead of surfacing
        /// as a foreign-key violation when the record is written.
        /// </summary>
        /// <param name="fieldMap">The payload carrying the class reference.</param>
        /// <param name="classId">
        /// When this method returns, contains the id of the referenced class, or
        /// <see cref="Guid.Empty"/> when the payload names none that exists.
        /// </param>
        /// <returns>True when the payload names an existing class; otherwise false.</returns>
        private static bool TryResolveClass(RestApiCrudFormData fieldMap, out Guid classId)
        {
            if (!fieldMap.TryGetGuid(nameof(Model.Entities.Template.ClassId), out classId) ||
                CoreHub.ClassManager.GetClass(classId) is null)
            {
                classId = Guid.Empty;

                return false;
            }

            return true;
        }
    }
}
