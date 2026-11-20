using KleeneStar.Core.WebControl;
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
    public sealed class Index : global::KleeneStar.Core.WebRestApi.RestApiCrudClassStructure<Model.Entities.Template>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the class a record belongs to, which decides who may change it.
        /// </summary>
        /// <param name="item">The record.</param>
        /// <returns>The class id.</returns>
        protected override System.Guid? ClassOf(Model.Entities.Template item)
        {
            return item?.ClassId;
        }

        /// <summary>
        /// Determines whether the caller may create a template: they must administer every class
        /// the payload names (<see cref="TemplatePresetForm.ClassesField"/>, else <c>ClassId</c>).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the template may be created.</returns>
        protected override bool MayCreate(IRequest request)
        {
            var classIds = TemplatePresetForm.ParseClassIds(ContentAuthorization.ReadPayloadText(request, TemplatePresetForm.ClassesField));

            if (classIds.Count == 0)
            {
                return base.MayCreate(request);
            }

            return classIds.All(x => ContentAuthorization.MayAdminister(x, request));
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
                AssigneeId = data?.AssigneeId,
                PredecessorId = data?.PredecessorId,
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
            var result = AvatarStorage.Validate(payload, nameof(Model.Entities.Template.Icon), base.Validate(existingItem, payload, request), existingItem?.Icon);
            var errors = new List<RestApiError>();

            // a template is bound to at least one class, so a create without a resolvable class
            // would otherwise reach the database and fail there on the foreign key. Clone and
            // update take the classes from the existing record unless the payload names them.
            errors.Add(ValidateClasses(existingItem, payload, request));
            errors.Add(ValidateCategories(payload, request));

            // the parent reference is rejected here rather than at the database, which knows only
            // that the row exists and nothing about workspaces or cycles
            errors.Add(ValidateHierarchy(existingItem, payload, nameof(Model.Entities.Template.ParentId), request));
            errors.Add(ValidatePredecessor(existingItem, payload, request));
            errors.Add(ValidateAssignee(payload, request));

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
            newItem.Category = TemplateCategories.Normalize(newItem.Category);

            // an inline picture of the dialog is stored under the new id
            newItem.Icon = AvatarStorage.Adopt(id, newItem.Icon, () => CoreHub.GenerateIcon(id));

            // the classes are taken from the payload only where they name ones that exist, so an
            // unknown id becomes an empty reference the validation reports rather than a
            // dangling one the insert fails on. The first named class owns the template.
            var classIds = ResolveClasses(fieldMap);
            newItem.ClassId = classIds.FirstOrDefault();

            if (TemplatePresetForm.Carries(fieldMap.Keys))
            {
                newItem.Presets = TemplatePresetForm.Collect(fieldMap, classIds, null, request.Culture);
            }

            CoreHub.TemplateManager.AddTemplate(newItem);
            CoreHub.TemplateManager.SetFurtherClasses(newItem.Id, classIds.Skip(1));

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
            newItem.Category = TemplateCategories.Normalize(newItem.Category);

            // an inline picture of the dialog is stored under the new id
            newItem.Icon = AvatarStorage.Adopt(id, newItem.Icon, () => CoreHub.GenerateIcon(id));

            // the classes are not part of the clone form; carrying them over from the source
            // keeps the copy serving the same classes and its presets meaningful
            var classIds = existingItem is not null
                ? CoreHub.TemplateManager.GetClassIds(existingItem.Id)
                : ResolveClasses(fieldMap);
            newItem.ClassId = classIds.FirstOrDefault();

            CoreHub.TemplateManager.AddTemplate(newItem);
            CoreHub.TemplateManager.SetFurtherClasses(newItem.Id, classIds.Skip(1));

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Gets the image formats the avatar dialog may send (<see cref="AvatarStorage.Options"/>).
        /// </summary>
        protected override RestApiAvatarUploadOptions ImageOptions => AvatarStorage.Options;

        /// <summary>
        /// Stores the picture an update sends in the icons directory (<see cref="AvatarStorage.Store"/>).
        /// </summary>
        /// <param name="item">The persisted record; the source on a clone, null on a create.</param>
        /// <param name="field">The property the picture belongs to.</param>
        /// <param name="image">The decoded and checked picture.</param>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The address of the stored picture, or null when nothing was stored.</returns>
        protected override string StoreImage(Model.Entities.Template item, string field, RestApiAvatarUpload image, IRequest request)
        {
            return AvatarStorage.Store(item?.Id, image, request);
        }

        /// <summary>
        /// Removes the stored pictures when an update empties the picture (<see cref="AvatarStorage.Remove"/>).
        /// </summary>
        /// <param name="item">The persisted record; the source on a clone, null on a create.</param>
        /// <param name="field">The property whose picture is removed.</param>
        /// <param name="request">The HTTP request.</param>
        protected override void RemoveImage(Model.Entities.Template item, string field, IRequest request)
        {
            AvatarStorage.Remove(item?.Id, request);
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
            var stored = CoreHub.TemplateManager.GetStoredPresets(existingItem.Id);
            var classIds = CoreHub.TemplateManager.GetClassIds(existingItem.Id);
            var owner = existingItem.ClassId;
            var res = base.Update(existingItem, payload, request);

            // the binding takes nothing but what the payload names; the owning class is moved
            // only through the class selection, which keeps the current owner where it can
            existingItem.ClassId = owner;
            existingItem.Category = TemplateCategories.Normalize(existingItem.Category);

            if (payload.ContainsKey(TemplatePresetForm.ClassesField) && ResolveClasses(payload) is { Count: > 0 } named)
            {
                classIds = named.Contains(owner) ? [owner, .. named.Where(x => x != owner)] : named;
                existingItem.ClassId = classIds[0];
            }

            if (TemplatePresetForm.Carries(payload.Keys))
            {
                existingItem.Presets = TemplatePresetForm.Collect(payload, classIds, stored, request.Culture);
            }

            // an emptied picture binds null; the record then carries its generated icon
            existingItem.Icon ??= CoreHub.GenerateIcon(existingItem.Id);

            CoreHub.TemplateManager.UpdateTemplate(existingItem);
            CoreHub.TemplateManager.SetFurtherClasses(existingItem.Id, classIds.Skip(1));

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
                ?? (ResolveClasses(payload) is { Count: > 0 } classIds
                    ? CoreHub.ClassManager.GetClass(classIds[0])?.WorkspaceId
                    : null);

            if (reference is null)
            {
                return new RestApiError
                (
                    I18N.Translate(request, "kleenestar.core:template.hierarchy.workspace"),
                    field: field
                );
            }

            // a work structure may reach into other workspaces - the onboarding of HR creates the
            // account request at the IT desk - but only with the consent of both sides: whoever
            // links a template of their workspace below a template of another must administer
            // that one too, because the link changes what it creates. The objects are later
            // created on the template's authority, so this is where that authority is checked.
            var referenceClass = reference.Class ?? CoreHub.ClassManager.GetClass(reference.ClassId);

            if (workspaceId is not null
                && referenceClass?.WorkspaceId != workspaceId
                && !ContentAuthorization.MayAdminister(reference.ClassId, request))
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
        /// Validates the predecessor of the payload: a sibling - a template below the same parent
        /// - other than the template itself.
        /// </summary>
        /// <remarks>
        /// The dependency becomes a blocking relation between two objects one instantiation
        /// creates, so it can only point at a template that instantiation creates as well.
        /// </remarks>
        /// <param name="existingItem">The template being edited, or null while one is created.</param>
        /// <param name="payload">The payload carrying the reference.</param>
        /// <param name="request">The request, for localizing the message.</param>
        /// <returns>The validation error, or null when the reference is valid or absent.</returns>
        private static RestApiError ValidatePredecessor(Model.Entities.Template existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var field = nameof(Model.Entities.Template.PredecessorId);

            if (!payload.TryGetGuidReference(field, out var predecessorId) || predecessorId is null)
            {
                return null;
            }

            var parentId = payload.TryGetGuidReference(nameof(Model.Entities.Template.ParentId), out var payloadParent)
                ? payloadParent
                : existingItem?.ParentId;
            var predecessor = CoreHub.TemplateManager.GetTemplate(predecessorId.Value);

            if (predecessor is null
                || predecessor.Id == existingItem?.Id
                || parentId is null
                || predecessor.ParentId != parentId)
            {
                return new RestApiError
                (
                    I18N.Translate(request, "kleenestar.core:template.predecessor.sibling"),
                    field: field
                );
            }

            return null;
        }

        /// <summary>
        /// Validates the assignee of the payload: an active account.
        /// </summary>
        /// <param name="payload">The payload carrying the reference.</param>
        /// <param name="request">The request, for localizing the message.</param>
        /// <returns>The validation error, or null when the reference is valid or absent.</returns>
        private static RestApiError ValidateAssignee(RestApiCrudFormData payload, IRequest request)
        {
            var field = nameof(Model.Entities.Template.AssigneeId);

            if (!payload.TryGetGuidReference(field, out var assigneeId) || assigneeId is null)
            {
                return null;
            }

            return CoreHub.IdentityManager.GetIdentity(assigneeId.Value) is { State: IdentityState.Active }
                ? null
                : new RestApiError(I18N.Translate(request, "kleenestar.core:template.assignee.unknown"), field: field);
        }

        /// <summary>
        /// Validates the classes of the payload: a create names at least one, every named class
        /// exists, all of them belong to one workspace (the template's, on an update), and the
        /// caller administers each of them.
        /// </summary>
        /// <param name="existingItem">The template being edited, or null while one is created.</param>
        /// <param name="payload">The payload carrying the classes.</param>
        /// <param name="request">The request, for localizing the message and judging the caller.</param>
        /// <returns>The validation error, or null when the classes are valid or not named.</returns>
        private static RestApiError ValidateClasses(Model.Entities.Template existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var field = TemplatePresetForm.ClassesField;
            var names = NamedClasses(payload);

            if (names is null)
            {
                return existingItem is null
                    ? new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.required"), field: field)
                    : null;
            }

            if (names.Count == 0)
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.required"), field: field);
            }

            var classes = names.Select(CoreHub.ClassManager.GetClass).ToList();

            if (classes.Any(x => x is null))
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.unknown"), field: field);
            }

            var workspaceId = existingItem is not null
                ? (existingItem.Class ?? CoreHub.ClassManager.GetClass(existingItem.ClassId))?.WorkspaceId
                : classes[0].WorkspaceId;

            if (classes.Any(x => x.WorkspaceId != workspaceId))
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.workspace"), field: field);
            }

            if (classes.Any(x => !ContentAuthorization.MayAdminister(x.Id, request)))
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.forbidden"), field: field);
            }

            return null;
        }

        /// <summary>
        /// Validates the categories of the payload: stored separated by semicolons, they have to
        /// fit the column (<see cref="TemplateCategories.MaxLength"/>).
        /// </summary>
        /// <param name="payload">The payload carrying the categories.</param>
        /// <param name="request">The request, for localizing the message.</param>
        /// <returns>The validation error, or null when the categories fit or are not named.</returns>
        private static RestApiError ValidateCategories(RestApiCrudFormData payload, IRequest request)
        {
            var field = nameof(Model.Entities.Template.Category);

            if (!payload.TryGetValue(field, out var value)
                || (TemplateCategories.Normalize(value?.ToString())?.Length ?? 0) <= TemplateCategories.MaxLength)
            {
                return null;
            }

            return new RestApiError(I18N.Translate(request, "kleenestar.core:template.category.length", TemplateCategories.MaxLength), field: field);
        }

        /// <summary>
        /// Reads the classes a payload names: the class selection
        /// (<see cref="TemplatePresetForm.ClassesField"/>), else a single <c>ClassId</c> as an
        /// API client sends it.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <returns>The named ids in order, or null when the payload names no classes at all.</returns>
        private static IReadOnlyList<Guid> NamedClasses(RestApiCrudFormData fieldMap)
        {
            if (fieldMap.TryGetValue(TemplatePresetForm.ClassesField, out var classes))
            {
                return TemplatePresetForm.ParseClassIds(classes);
            }

            if (fieldMap.TryGetValue(nameof(Model.Entities.Template.ClassId), out var classId))
            {
                return TemplatePresetForm.ParseClassIds(classId);
            }

            return null;
        }

        /// <summary>
        /// Resolves the classes a payload binds a template to, keeping those that exist, so an
        /// unknown or missing reference is reported by the validation instead of surfacing as a
        /// foreign-key violation when the record is written.
        /// </summary>
        /// <param name="fieldMap">The payload carrying the classes.</param>
        /// <returns>The existing classes in the order named; the first owns the template.</returns>
        private static IReadOnlyList<Guid> ResolveClasses(RestApiCrudFormData fieldMap)
        {
            return [.. (NamedClasses(fieldMap) ?? []).Where(x => CoreHub.ClassManager.GetClass(x) is not null)];
        }
    }
}
