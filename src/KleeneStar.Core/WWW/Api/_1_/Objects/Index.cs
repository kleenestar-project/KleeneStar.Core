using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// Provides CRUD operations for object items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the dialog reads (<c>mode=new|clone|edit|delete</c>) only to a caller who may
        /// make the change the dialog is for.
        /// </summary>
        /// <remarks>
        /// A plain read needs no gate here: the object manager already leaves out what the caller
        /// may not read, so such an object is answered as not found.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);

            var authorized = request?.GetParameter("mode")?.Value switch
            {
                "edit" or "delete" or "clone" => ContentAuthorization.MayWrite(Resolve(id), request),
                "new" => id is null
                    ? ContentAuthorization.MayCreateIn(ClassOfCreate(request), request)
                    : ContentAuthorization.MayWrite(Resolve(id), request),
                _ => true
            };

            return authorized ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Creates an object in a class the caller may write in, or clones one they may change.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);
            var authorized = id is null
                ? ContentAuthorization.MayCreateIn(ClassOfCreate(request), request)
                : ContentAuthorization.MayWrite(Resolve(id), request);

            return authorized ? base.Create(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Changes an object, once the caller may.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return ContentAuthorization.MayWrite(Resolve(ContentAuthorization.ReadId(request)), request)
                ? base.Update(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Deletes an object, once the caller may change it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return ContentAuthorization.MayWrite(Resolve(ContentAuthorization.ReadId(request)), request)
                ? base.Delete(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Returns the class a create is made in: the one the payload names, else the one of
        /// the template it names - the wizard sends a template, not always a class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The class id, or <see langword="null"/>.</returns>
        private static Guid? ClassOfCreate(IRequest request)
        {
            return ContentAuthorization.ReadPayloadGuid(request, "classid")
                ?? (ContentAuthorization.ReadPayloadGuid(request, "templateid") is { } templateId
                    ? CoreHub.TemplateManager.GetTemplate(templateId)?.ClassId
                    : null);
        }

        /// <summary>
        /// Resolves the object an id addresses, as the caller may see it.
        /// </summary>
        /// <param name="id">The id, may be absent.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static Model.Entities.Object Resolve(Guid? id)
        {
            return id is { } objectId ? CoreHub.ObjectManager.GetObject(objectId) : null;
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
        protected override IEnumerable<Model.Entities.Object> Retrieve(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(query, context);
        }

        /// <summary>
        /// Retrieves the data required to create a new workspace entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new workspace for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            return base.RetrieveForCreate(request);
        }

        /// <summary>
        /// Retrieves a result object containing default values and metadata for
        /// cloning a item.
        /// </summary>
        /// <remarks>
        /// In addition to the system properties of the source object, the response also
        /// carries the persisted per-field <see cref="Model.Entities.Value"/> rows, keyed
        /// by the field name. This lets the dynamic form inputs built from the active
        /// edit form pre-populate via the form's REST data binding instead of starting
        /// blank.
        /// </remarks>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">The request.</param>
        /// <returns>
        /// A result instance representing the data and metadata required
        /// to initialize a new item for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Object> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ObjectManager.GetObjects(query, context)
                .FirstOrDefault();

            if (data is null)
            {
                return RetrieveForClone(request, null);
            }

            var newItem = new Model.Entities.Object()
            {
                Summary = data.Summary + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                State = WorkspaceState.Active,
                WorkspaceId = data.WorkspaceId,
                ClassId = data.ClassId,
                ParentId = data.ParentId,

                // the copy of a classified record is classified the same way: a duplicate that
                // quietly came out readable by more people than its original would be a leak
                // dressed up as a convenience
                SecurityLevelId = data.SecurityLevelId
            };

            var result = RetrieveForClone(request, newItem);
            MergeFieldValues(result, data.Id, data.ClassId);
            return result;
        }

        /// <summary>
        /// Retrieves a workspace identified by the specified key for update operations.
        /// </summary>
        /// <remarks>
        /// In addition to the system properties of the object, the response also carries
        /// the persisted per-field <see cref="Model.Entities.Value"/> rows, keyed by the
        /// field name. This lets the dynamic form inputs built from the active edit form
        /// pre-populate via the form's REST data binding instead of starting blank.
        /// </remarks>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the workspace associated with the specified key.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Object> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ObjectManager.GetObjects(query, context)
                .FirstOrDefault();

            var result = RetrieveForUpdate(request, data);

            if (data is not null)
            {
                MergeFieldValues(result, data.Id, data.ClassId);
            }

            return result;
        }

        /// <summary>
        /// Adds the persisted field values of the specified object to the JSON data
        /// dictionary returned by the base CRUD retrieval, keyed by field name so the
        /// dynamic form inputs can bind them by name. Inactive or deprecated fields are
        /// skipped to match the structure rendered by the edit form. Existing entries
        /// in the dictionary (system properties such as <c>Summary</c>, <c>Description</c>)
        /// are left untouched.
        /// </summary>
        /// <param name="result">The retrieve result whose <c>Data</c> dictionary is to
        /// be augmented. No-op when the data is not a string-keyed dictionary.</param>
        /// <param name="objectId">The id of the object whose values to merge.</param>
        /// <param name="classId">The id of the object's class, used to look up the
        /// field definitions for name + filtering.</param>
        private static void MergeFieldValues(IRestApiCrudResultRetrieve result, Guid objectId, Guid classId)
        {
            if (result?.Data is not IDictionary<string, object> data)
            {
                return;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new WebParameter.ClassIdParameter(classId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToDictionary(f => f.Id);

            foreach (var value in CoreHub.ValueManager.GetValues(objectId))
            {
                if (!fields.TryGetValue(value.FieldId, out var field))
                {
                    continue;
                }

                data[field.Name] = value.Data;
            }
        }

        /// <summary>
        /// Retrieves the workspace entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the workspace entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Object> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ObjectManager.GetObjects(query, context)
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
        protected override IRestApiValidationResult Validate(Model.Entities.Object existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);

            return ValidateSecurityLevel(existingItem, payload, request, result);
        }

        /// <summary>
        /// Refuses a classification the caller may not put on the object.
        /// </summary>
        /// <remarks>
        /// The form only offers the levels the caller is cleared for, so this is not what stops
        /// an honest mistake - it is what makes the rule true of the endpoint rather than of one
        /// dialog. Two things are checked: that the level belongs to the class of the object,
        /// and that the caller is cleared for it. Clearing the classification is always allowed:
        /// it makes the record more visible, never less.
        /// </remarks>
        /// <param name="existingItem">The currently persisted item (null for create).</param>
        /// <param name="payload">The submitted payload.</param>
        /// <param name="request">The request, for the culture of the message.</param>
        /// <param name="result">The validation result to add to.</param>
        /// <returns>The validation result, for chaining.</returns>
        private static IRestApiValidationResult ValidateSecurityLevel(Model.Entities.Object existingItem, RestApiCrudFormData payload, IRequest request, IRestApiValidationResult result)
        {
            if (!payload.TryGetGuid(nameof(Model.Entities.Object.SecurityLevelId), out var securityLevelId)
                || securityLevelId == Guid.Empty)
            {
                return result;
            }

            // an unchanged classification is not a new decision and is left alone, so an edit of
            // a record somebody was cleared for yesterday does not become unsavable today
            if (existingItem?.SecurityLevelId == securityLevelId)
            {
                return result;
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var classId = existingItem?.ClassId
                ?? (payload.TryGetGuid(nameof(Model.Entities.Object.ClassId), out var payloadClassId) ? payloadClassId : Guid.Empty);

            var assignable = classId == Guid.Empty
                ? []
                : CoreHub.SecurityLevelManager.GetAssignableSecurityLevels(classId, identityId);

            if (assignable.Any(x => x.Id == securityLevelId))
            {
                return result;
            }

            return result.Add
            (
                I18N.Translate(request, "kleenestar.core:securitylevel.object.restricted"),
                nameof(Model.Entities.Object.SecurityLevelId),
                "securitylevel.restricted"
            );
        }

        /// <summary>
        /// Resolves the identity a new object is attributed to, as the value its creator and
        /// updater references take.
        /// </summary>
        /// <remarks>
        /// An anonymous caller is nobody, and nobody is not a row in the identity table:
        /// written as the empty guid the creator broke the foreign key and the create answered
        /// a bare <em>error creating resource</em>, so the references stay unset instead - the
        /// way the update path and the workspace templates already leave them. The commit the
        /// creation is recorded in makes the same distinction on its own.
        /// </remarks>
        /// <param name="request">The request, or null to ask the ambient scope.</param>
        /// <returns>The identity, or null for an anonymous caller.</returns>
        public static Guid? ResolveAuthor(IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return identityId == Guid.Empty ? null : identityId;
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
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Object newItem)
        {
            var id = Guid.NewGuid();
            var currentUser = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var author = ResolveAuthor(request);

            newItem = new Model.Entities.Object(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = WorkspaceState.Active,
                CreatorId = author,
                UpdaterId = author
            };

            fieldMap.BindTo(newItem);

            DeriveReferences(fieldMap, newItem);
            PlaceBelowOpenDocument(fieldMap, newItem);
            EnsureKey(newItem);
            ApplyDefaultSecurityLevel(fieldMap, newItem, currentUser);

            var template = ResolveTemplate(fieldMap);
            var presets = template is null
                ? new Dictionary<string, string>()
                : CoreHub.TemplateManager.GetPresets(template.Id);

            // the creation runs under the client's key: a second submission of the same dialog
            // is answered with what the first created, and a failure half-way through a work
            // structure takes back what was written instead of leaving a fragment
            var run = ObjectCreationRun.Claim(ReadIdempotencyKey(fieldMap, request), template?.Id, currentUser, out var existing);

            if (run is null)
            {
                if (existing.State == ObjectCreationState.Completed
                    && existing.RootObjectId is Guid rootId
                    && CoreHub.ObjectManager.GetObject(rootId) is { } created)
                {
                    newItem = created;

                    return Created(created);
                }

                throw new RestApiRefusal(I18N.Translate(request, "kleenestar.core:object.create.running"));
            }

            // the summary and the description are columns of the object row, not value rows,
            // so a preset on them has to land before the row is written - the genesis commit
            // then records the object the way the template meant it
            ApplyPresetProperties(newItem, presets);

            try
            {
                // the object row, its field values and the presets of its template are one act
                // of creation; the scope makes them one genesis commit rather than a create
                // followed by a handful of edits nobody performed
                using (CoreHub.CommitManager.BeginCommit(newItem.Id, CommitType.Created, currentUser))
                {
                    CoreHub.ObjectManager.Add(newItem);
                    run.TrackObject(newItem.Id);

                    UpsertFieldValues(newItem, fieldMap);

                    ApplyPresetValues(newItem, presets, fieldMap);
                }

                if (template is not null)
                {
                    CreateChildren(newItem, template.Id, request, new HashSet<Guid> { template.Id }, run);
                }

                RelateToAggregate(fieldMap, newItem, request, currentUser, run);

                run.Complete(newItem.Id);
            }
            catch (Exception ex)
            {
                run.RollBack(ex);

                throw;
            }

            return Created(newItem);
        }

        /// <summary>
        /// Reads the idempotency key a creation was submitted under: the wizard's hidden field,
        /// or the <c>idempotencykey</c> query parameter of a REST client.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The key, or <see langword="null"/> when the client sent none.</returns>
        private static string ReadIdempotencyKey(RestApiCrudFormData fieldMap, IRequest request)
        {
            var field = Convert.ToString
            (
                fieldMap.FirstOrDefault(x => string.Equals(x.Key, global::KleeneStar.Core.WebFragment.Object.ObjectAddFormFragment.IdempotencyKeyField, StringComparison.OrdinalIgnoreCase)).Value,
                CultureInfo.InvariantCulture
            );

            if (!string.IsNullOrWhiteSpace(field))
            {
                return field;
            }

            // the framework exposes no arbitrary request header, so a client that cannot put the
            // key into the payload names it in the query
            return request?.GetParameter("idempotencykey")?.Value;
        }

        /// <summary>
        /// Files a new object as a sub-task of the object the wizard was opened from.
        /// </summary>
        /// <remarks>
        /// The sub-task is joined to its order by the first active relation type whose effect
        /// aggregates progress - the relation model's answer to "is part of", which, unlike the
        /// containment of <c>ParentId</c>, crosses workspaces: an order filed in one department
        /// reports the progress of the sub-tasks the others work on in theirs. The order must be
        /// one the caller may read and relate (<see cref="global::KleeneStar.Core.WebPermissions.ObjectRelationPermission"/>); otherwise
        /// the object is created on its own, which is what the caller asked for first.
        /// </remarks>
        /// <param name="fieldMap">The payload, which may name the order.</param>
        /// <param name="object">The object just created.</param>
        /// <param name="request">The request.</param>
        /// <param name="identityId">The identity creating it.</param>
        /// <param name="run">The creation the relation is part of.</param>
        private static void RelateToAggregate(RestApiCrudFormData fieldMap, Model.Entities.Object @object, IRequest request, Guid identityId, ObjectCreationRun run)
        {
            var key = Convert.ToString
            (
                fieldMap.FirstOrDefault(x => string.Equals(x.Key, global::KleeneStar.Core.WebFragment.Object.ObjectAddFormFragment.SubtaskOfField, StringComparison.OrdinalIgnoreCase)).Value,
                System.Globalization.CultureInfo.InvariantCulture
            );

            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var order = CoreHub.ObjectManager.GetObjectByKey(key);

            if (order is null || order.Id == @object.Id
                || !ContentAuthorization.MayWrite(order, request, typeof(global::KleeneStar.Core.WebPermissions.ObjectRelationPermission)))
            {
                return;
            }

            Relate(WebExpress.WebApp.WebRelation.RelationEffect.AggregatesProgress, order.Id, @object.Id, identityId, run);
        }

        /// <summary>
        /// Relates two objects by the first active relation type carrying an effect.
        /// </summary>
        /// <remarks>
        /// The relation catalog is the installation's, and nothing here knows a relation by
        /// name: "is part of" is whichever type aggregates progress, "has to wait for" whichever
        /// blocks completion. An installation without such a type gets no relation rather than
        /// an error - the objects exist either way.
        /// </remarks>
        /// <param name="effect">The effect the relation carries.</param>
        /// <param name="sourceId">The source end - the order, the predecessor.</param>
        /// <param name="targetId">The target end - the sub-task, the successor.</param>
        /// <param name="identityId">The identity relating them.</param>
        /// <param name="run">The creation the relation is part of.</param>
        private static void Relate(WebExpress.WebApp.WebRelation.RelationEffect effect, Guid sourceId, Guid targetId, Guid identityId, ObjectCreationRun run)
        {
            var type = CoreHub.ObjectRelationTypeManager
                .GetRelationTypes()
                .Where(x => x.Active && x.Effect == effect
                    && string.Equals(x.System, WebExpress.WebApp.WebRelation.RelationSystem.Object, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Order)
                .FirstOrDefault();

            if (type is null)
            {
                return;
            }

            var relation = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = WebExpress.WebApp.WebRelation.RelationSystem.Object,
                TypeKey = type.Key,
                Direction = WebExpress.WebApp.WebRelation.RelationDirection.Bidirectional,
                Status = WebExpress.WebApp.WebRelation.RelationStatus.Active,
                SourceObjectId = sourceId,
                TargetObjectId = targetId,
                CreatedById = identityId == Guid.Empty ? null : identityId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ObjectRelationManager.Add(relation);
            run?.TrackRelation(relation.Id);
        }

        /// <summary>
        /// Creates a new instance by cloning data from the specified form fields and
        /// adds it to the class manager.
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
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Object existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Object newItem)
        {
            var id = Guid.NewGuid();
            var currentUser = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var author = ResolveAuthor(request);

            newItem = new Model.Entities.Object(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = WorkspaceState.Active,
                CreatorId = author,
                UpdaterId = author
            };

            fieldMap.BindTo(newItem);

            newItem.ClassId = existingItem?.ClassId ?? newItem.ClassId;
            newItem.WorkspaceId = existingItem?.WorkspaceId ?? newItem.WorkspaceId;

            // a copy keeps the classification of its original when the payload names none; see
            // RetrieveForClone for why it is not allowed to come out more visible
            newItem.SecurityLevelId ??= existingItem?.SecurityLevelId;

            DeriveReferences(fieldMap, newItem);
            EnsureKey(newItem);

            using (CoreHub.CommitManager.BeginCommit(newItem.Id, CommitType.Created, currentUser))
            {
                CoreHub.ObjectManager.Add(newItem);

                UpsertFieldValues(newItem, fieldMap);
            }

            return Created(newItem);
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
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Object existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var res = base.Update(existingItem, payload, request);

            // stamp the identity that performed this update (best-effort; keep the prior
            // updater when the request is unauthenticated so the FK never points at an
            // empty identity).
            var currentUser = CoreHub.SessionManager.GetCurrentIdentityId(request);
            if (currentUser != Guid.Empty)
            {
                existingItem.UpdaterId = currentUser;
            }

            // one save is one commit, whether it moved a system property, a field value, or both
            using (CoreHub.CommitManager.BeginCommit(existingItem.Id, CommitType.Updated, currentUser))
            {
                CoreHub.ObjectManager.Update(existingItem);

                UpsertFieldValues(existingItem, payload);
            }

            return res;
        }

        /// <summary>
        /// Determines whether the payload answers a key with something other than a blank.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="key">The key, matched ignoring case.</param>
        /// <returns><see langword="true"/> when the payload carries a non-blank value.</returns>
        private static bool IsAnswered(RestApiCrudFormData fieldMap, string key)
        {
            return fieldMap.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Convert.ToString(x.Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Puts the class's default classification on an object the payload left unclassified.
        /// </summary>
        /// <remarks>
        /// A class that classifies its objects names one level as the starting point, and a
        /// create that says nothing about the classification means "the usual one" rather than
        /// "none" - otherwise every record filed through an interface that does not ask (the
        /// api, a template, an import) would silently come out unclassified.
        /// <para>
        /// The default is only applied when the caller is cleared for it. Where they are not,
        /// the object stays unclassified rather than disappearing from the list of the person
        /// who just created it.
        /// </para>
        /// </remarks>
        /// <param name="fieldMap">The payload carrying the answers.</param>
        /// <param name="object">The object being created.</param>
        /// <param name="identityId">The identity performing the create.</param>
        private static void ApplyDefaultSecurityLevel(RestApiCrudFormData fieldMap, Model.Entities.Object @object, Guid identityId)
        {
            // an explicit answer wins, including the explicit "unclassified" the selection
            // submits as the empty guid; a blank one is no answer - the wizard posts every input
            // of its last step, and a selection that did not take its preset must not leave a
            // classified class's record unclassified
            if (IsAnswered(fieldMap, nameof(Model.Entities.Object.SecurityLevelId))
                || @object.SecurityLevelId.HasValue
                || @object.ClassId == Guid.Empty)
            {
                return;
            }

            var securityLevel = CoreHub.SecurityLevelManager.GetDefaultSecurityLevel(@object.ClassId);

            if (securityLevel is not null && CoreHub.SecurityLevelManager.IsCleared(identityId, securityLevel.Id))
            {
                @object.SecurityLevelId = securityLevel.Id;
            }
        }

        /// <summary>
        /// Completes the references an object cannot be stored without from what the payload
        /// does name.
        /// </summary>
        /// <remarks>
        /// The references themselves are bound by <c>BindTo</c>. What it cannot do is derive
        /// the ones the create form leaves out because they follow from another: a create
        /// started from a template names only the template, one started from a workspace
        /// overview only the class.
        /// </remarks>
        /// <param name="fieldMap">The payload carrying the references.</param>
        /// <param name="object">The object to complete.</param>
        private static void DeriveReferences(RestApiCrudFormData fieldMap, Model.Entities.Object @object)
        {
            // an object created from a template inherits the class the template instantiates
            // when the payload names only the template
            if (@object.ClassId == Guid.Empty && fieldMap.TryGetGuid("TemplateId", out var templateId))
            {
                @object.ClassId = CoreHub.TemplateManager.GetTemplate(templateId)?.ClassId ?? Guid.Empty;
            }

            // an object created from a workspace overview inherits the workspace of its class when
            // the payload names only the class
            if (@object.WorkspaceId == Guid.Empty)
            {
                @object.WorkspaceId = CoreHub.ClassManager.GetClass(@object.ClassId)?.WorkspaceId ?? Guid.Empty;
            }
        }

        /// <summary>
        /// Answers a create with what the page needs to open the new object.
        /// </summary>
        /// <remarks>
        /// The pages around the create dialog are rendered once - the sidebar's page tree, the
        /// recent lists - and nothing re-renders them, so a new object would appear only after a
        /// reload. <c>objectcreated.js</c> reads this answer and opens the new object instead,
        /// which renders the page anew with the object in its place. <c>created</c> marks the
        /// answer as one of this endpoint; there is no <c>message</c>, which would keep the
        /// dialog open to show it.
        /// </remarks>
        /// <param name="object">The object that was created.</param>
        /// <returns>The create result.</returns>
        private static RestApiCrudResultCreate Created(Model.Entities.Object @object)
        {
            return new RestApiCrudResultCreate
            {
                Data = new
                {
                    created = true,
                    id = @object.Id,
                    key = @object.Key,
                    uri = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog.ResolveDetailUri(@object)?.ToString()
                }
            };
        }

        /// <summary>
        /// Places a new document below the document the create wizard was opened from.
        /// </summary>
        /// <remarks>
        /// The header's create button names the open document
        /// (<c>ObjectAddFormFragment.ParentKeyField</c>) whatever is created from it, so the
        /// rule is decided here: the new object goes below it only when it is a document
        /// itself and lives in the same workspace - an issue created while reading a page
        /// stays a root, and a page tree does not reach across workspaces. The document is
        /// read through the object manager, so one the caller may not read places nothing.
        /// A <c>ParentId</c> the payload names itself (an API client) is left as it was bound.
        /// </remarks>
        /// <param name="fieldMap">The payload, which may name the open document.</param>
        /// <param name="object">The object being created.</param>
        private static void PlaceBelowOpenDocument(RestApiCrudFormData fieldMap, Model.Entities.Object @object)
        {
            var key = fieldMap.FirstOrDefault(x => string.Equals(x.Key, global::KleeneStar.Core.WebFragment.Object.ObjectAddFormFragment.ParentKeyField, StringComparison.OrdinalIgnoreCase))
                .Value?.ToString();

            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var parent = CoreHub.ObjectManager.GetObjectByKey(key);
            var kind = CoreHub.ClassManager.GetClass(@object.ClassId)?.Kind;

            if (parent is not null
                && parent.WorkspaceId == @object.WorkspaceId
                && string.Equals(ObjectKind.Normalize(parent.Kind), ObjectKind.Document, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ObjectKind.Normalize(kind), ObjectKind.Document, StringComparison.OrdinalIgnoreCase))
            {
                @object.ParentId = parent.Id;
            }
        }

        /// <summary>
        /// Assigns the object a key when the payload carries none.
        /// </summary>
        /// <remarks>
        /// The key is the human-readable handle an object is addressed by (<c>SD-17</c>), so it
        /// has to exist before the record is written, and the create form does not ask for one.
        /// The numbering itself belongs to the object manager, because this is not the only
        /// caller that creates objects nobody named - the setup a workspace template performs is
        /// another - and a second implementation of it would eventually hand out the same key
        /// twice.
        /// </remarks>
        /// <param name="object">The object to assign a key to.</param>
        private static void EnsureKey(Model.Entities.Object @object)
        {
            if (!string.IsNullOrWhiteSpace(@object.Key))
            {
                return;
            }

            @object.Key = CoreHub.ObjectManager.NextObjectKey(@object.WorkspaceId) ?? @object.Key;
        }

        /// <summary>
        /// Resolves the template a create payload names, when it names one that can be applied.
        /// </summary>
        /// <remarks>
        /// The template step of the wizard submits <c>none</c> for "no template", which is not a
        /// guid and reads as absent; an archived template is offered nowhere and is not applied
        /// through a stale id either.
        /// </remarks>
        /// <param name="fieldMap">The payload, which may name a template.</param>
        /// <returns>The active template, or null when the payload names none.</returns>
        private static Model.Entities.Template ResolveTemplate(RestApiCrudFormData fieldMap)
        {
            if (!fieldMap.TryGetGuid("TemplateId", out var templateId))
            {
                return null;
            }

            var template = CoreHub.TemplateManager.GetTemplate(templateId);

            return template?.State == TemplateState.Active ? template : null;
        }

        /// <summary>
        /// Puts the presets a template holds for the summary and the description onto an object
        /// whose own are blank.
        /// </summary>
        /// <remarks>
        /// The two are the attributes every object carries on its own row; a create form asks
        /// for them under those names and <c>BindTo</c> writes them there, never into a value
        /// row - so <see cref="UpsertFieldValues"/> skips them by design and a preset on either
        /// would be lost without this. The caller's answer wins where there is one. The
        /// description preset is what the template manager answers for it: the template's own
        /// description unless its presets name one, so the text a template was written with is
        /// the text its objects start with.
        /// </remarks>
        /// <param name="object">The object being created.</param>
        /// <param name="presets">The presets of the template, keyed by field name.</param>
        private static void ApplyPresetProperties(Model.Entities.Object @object, IReadOnlyDictionary<string, string> presets)
        {
            if (IsBlank(@object.Summary) && TryGetPreset(presets, nameof(Model.Entities.Object.Summary), out var summary))
            {
                @object.Summary = summary;
            }

            if (IsBlank(@object.Description) && TryGetPreset(presets, nameof(Model.Entities.Object.Description), out var description))
            {
                @object.Description = description;
            }
        }

        /// <summary>
        /// Writes the presets of a template as field values of an object, skipping the fields the
        /// payload answered.
        /// </summary>
        /// <remarks>
        /// A value the caller submitted wins over the preset that would otherwise fill the same
        /// field - a template pre-fills a form, it does not overrule what the user typed into
        /// it. What the caller submitted has to be an answer, though: the wizard posts every
        /// input of the create form, so a field the form could not pre-fill (a choice the
        /// template names by a value the field does not offer, a control that ignores a seeded
        /// value) arrives as an empty string, and a payload that merely mentions the field used
        /// to shadow the preset - the object then came out without the very value the template
        /// was picked for. Blank is treated as unanswered, and the preset lands.
        /// </remarks>
        /// <param name="object">The object to write the values to.</param>
        /// <param name="presets">The presets of the template, keyed by field name.</param>
        /// <param name="payload">The payload whose own answers take precedence, or null.</param>
        private static void ApplyPresetValues(Model.Entities.Object @object, IReadOnlyDictionary<string, string> presets, RestApiCrudFormData payload)
        {
            var values = new RestApiCrudFormData();

            foreach (var preset in presets)
            {
                var key = preset.Key.ToLowerInvariant();

                if (payload is not null
                    && payload.TryGetValue(key, out var answer)
                    && !IsBlank(SerializePayloadValue(answer)))
                {
                    continue;
                }

                values[key] = preset.Value;
            }

            UpsertFieldValues(@object, values);
        }

        /// <summary>
        /// Reads a preset by field name, ignoring case - the presets are keyed the way the
        /// template author wrote them, the payload keys arrive lower-cased.
        /// </summary>
        /// <param name="presets">The presets of the template.</param>
        /// <param name="name">The field name.</param>
        /// <param name="value">When this method returns, the preset, or null.</param>
        /// <returns>True when the template presets the field with a non-blank value.</returns>
        private static bool TryGetPreset(IReadOnlyDictionary<string, string> presets, string name, out string value)
        {
            value = presets
                .FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase))
                .Value;

            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Decides whether a submitted value says nothing.
        /// </summary>
        /// <remarks>
        /// Null and whitespace are blank, and so is the document the prose editor submits for a
        /// surface nobody wrote into: an editor posts a document rather than an empty string,
        /// so a rich-text field the form could not pre-fill would otherwise arrive as an answer
        /// and shadow the preset it was supposed to start from. A document carrying a picture
        /// is not blank - <see cref="ProseText.IsEmpty"/> measures what is in it.
        /// </remarks>
        /// <param name="value">The serialized value.</param>
        /// <returns>True when the value carries nothing.</returns>
        private static bool IsBlank(string value)
        {
            return ProseText.IsEmpty(value);
        }

        /// <summary>
        /// Creates one object per active child template of the supplied object, depth first and
        /// in the order the child templates define - a work structure.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A child is filed where its class lives.</b> A composite template may name classes
        /// of other workspaces - an onboarding filed in HR creates the account request in the IT
        /// desk and the access card in facility management - so the child takes the workspace of
        /// its class. It is placed below its parent (<c>ParentId</c>) only inside the same
        /// workspace and where the parent's class allows the child's; the containment is
        /// workspace-bound on purpose. Every child is joined to its parent by the aggregating
        /// relation as well, so the parent reports the progress of the whole structure across
        /// workspaces. A same-workspace child the parent's class does not allow is skipped, as
        /// before, so a composite template cannot build a hierarchy the object model rejects.
        /// </para>
        /// <para>
        /// <b>Who does it and what it waits for.</b> A child template's assignee is the child's
        /// (<see cref="Template.AssigneeId"/>), and a predecessor among the siblings becomes a
        /// blocking relation from the predecessor's object to the child's
        /// (<see cref="Template.PredecessorId"/>), so the workflow refuses to finish the child
        /// first.
        /// </para>
        /// <para>
        /// The children are created on the template's authority: the cross-workspace classes
        /// were authorized when the template was saved (the author had to be allowed to create
        /// in them), not by the person filing the structure. Every object and relation is tracked
        /// by the run, which takes them back if anything fails. The visited set carries the
        /// templates already instantiated along this branch, which keeps a cycle in the template
        /// hierarchy from creating objects without end.
        /// </para>
        /// </remarks>
        /// <param name="parent">The object the created objects belong to.</param>
        /// <param name="templateId">The template whose children are instantiated.</param>
        /// <param name="request">The request, for resolving the acting identity.</param>
        /// <param name="visited">The templates already instantiated along this branch.</param>
        /// <param name="run">The creation the objects are part of.</param>
        private static void CreateChildren(Model.Entities.Object parent, Guid templateId, IRequest request, ISet<Guid> visited, ObjectCreationRun run)
        {
            var parentClass = CoreHub.ClassManager.GetClass(parent.ClassId);
            var currentUser = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var author = ResolveAuthor(request);
            var createdBy = new Dictionary<Guid, Guid>();
            var dependencies = new List<(Guid PredecessorTemplateId, Guid ObjectId)>();

            foreach (var childTemplate in CoreHub.TemplateManager.GetChildTemplates(templateId))
            {
                if (!visited.Add(childTemplate.Id))
                {
                    continue;
                }

                var childClass = CoreHub.ClassManager.GetClass(childTemplate.ClassId);

                if (childClass is null)
                {
                    continue;
                }

                var contained = childClass.WorkspaceId == parent.WorkspaceId;

                if (contained
                    && parentClass?.AllowedChildren is { Count: > 0 }
                    && parentClass.AllowedChildren.All(c => c.Id != childTemplate.ClassId))
                {
                    continue;
                }

                // nobody answers for a child, so every preset applies. The summary is the
                // template's name unless a preset says otherwise; the description is what the
                // template manager answers for it - the template's own unless a preset names one
                var presets = CoreHub.TemplateManager.GetPresets(childTemplate.Id);
                var id = Guid.NewGuid();
                var child = new Model.Entities.Object(id)
                {
                    Summary = TryGetPreset(presets, nameof(Model.Entities.Object.Summary), out var summary)
                        ? summary
                        : childTemplate.Name,
                    Description = TryGetPreset(presets, nameof(Model.Entities.Object.Description), out var description)
                        ? description
                        : null,
                    Icon = childTemplate.Icon ?? CoreHub.GenerateIcon(id),
                    State = WorkspaceState.Active,
                    ClassId = childTemplate.ClassId,
                    WorkspaceId = childClass.WorkspaceId,
                    ParentId = contained ? parent.Id : null,
                    AssigneeId = childTemplate.AssigneeId,
                    SecurityLevelId = CoreHub.SecurityLevelManager?.GetDefaultSecurityLevel(childTemplate.ClassId)?.Id,
                    CreatorId = author,
                    UpdaterId = author
                };

                EnsureKey(child);

                // a child gets a genesis commit of its own carrying its presets, so its history
                // starts the same way a hand-created object's does
                using (CoreHub.CommitManager.BeginCommit(child.Id, CommitType.Created, currentUser))
                {
                    CoreHub.ObjectManager.Add(child);
                    run?.TrackObject(child.Id);

                    ApplyPresetValues(child, presets, null);
                }

                Relate(WebExpress.WebApp.WebRelation.RelationEffect.AggregatesProgress, parent.Id, child.Id, currentUser, run);

                createdBy[childTemplate.Id] = child.Id;

                if (childTemplate.PredecessorId is Guid predecessorId)
                {
                    dependencies.Add((predecessorId, child.Id));
                }

                CreateChildren(child, childTemplate.Id, request, visited, run);
            }

            // the dependencies are drawn once all siblings exist, so a predecessor ordered after
            // its successor is found all the same
            foreach (var (predecessorTemplateId, objectId) in dependencies)
            {
                if (createdBy.TryGetValue(predecessorTemplateId, out var predecessorObjectId) && predecessorObjectId != objectId)
                {
                    Relate(WebExpress.WebApp.WebRelation.RelationEffect.BlocksCompletion, predecessorObjectId, objectId, currentUser, run);
                }
            }
        }

        /// <summary>
        /// Persists every payload entry that maps to a configured <see cref="Field"/> of
        /// the object's class as a <see cref="Model.Entities.Value"/> row.
        /// </summary>
        /// <remarks>
        /// The base <see cref="RestApiCrudFormData"/> binder only writes payload entries
        /// that match a public property of <see cref="Model.Entities.Object"/>; any other
        /// key (typically a field name like <c>AffectedCI</c>) is silently dropped. The
        /// inline <c>ControlSmartEdit</c> on the object detail page (see
        /// <c>ObjectItemDetailFragment</c>) PUTs exactly such payloads — a single
        /// <c>{ "FieldName": "new value" }</c> document per edit — so this method fills
        /// the gap by upserting the matching <see cref="Model.Entities.Value"/> row.
        /// Payload keys arrive in lower case (see
        /// <c>JsonExtensionsFieldMap.ToFieldMap</c>); the lookup honours that by
        /// lowering the field names before comparison.
        /// </remarks>
        /// <param name="object">The object whose field values are written.</param>
        /// <param name="payload">The payload carrying the values.</param>
        private static void UpsertFieldValues(Model.Entities.Object @object, RestApiCrudFormData payload)
        {
            if (@object is null || payload is null || payload.Count == 0)
            {
                return;
            }

            var systemProps = typeof(Model.Entities.Object)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name.ToLowerInvariant())
                .ToHashSet();

            var fieldsByName = CoreHub.FieldManager
                .GetFields(new WebParameter.ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToDictionary(f => f.Name.ToLowerInvariant(), f => f);

            // load the object's existing values once and index them by field, rather than
            // issuing one ValueManager.GetValue(objectId, fieldId) query per payload entry.
            var existingByField = CoreHub.ValueManager
                .GetValues(@object.Id)
                .GroupBy(v => v.FieldId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var kv in payload)
            {
                if (systemProps.Contains(kv.Key))
                {
                    // already handled by RestApiCrudFormData.BindTo
                    continue;
                }

                if (!fieldsByName.TryGetValue(kv.Key, out var field))
                {
                    // unknown / removed / deprecated field — drop silently
                    continue;
                }

                var raw = Normalize(SerializePayloadValue(kv.Value), field.FieldType);
                existingByField.TryGetValue(field.Id, out var existing);

                if (existing is null)
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        continue;
                    }

                    CoreHub.ValueManager.Add(new Model.Entities.Value
                    {
                        ObjectId = @object.Id,
                        FieldId = field.Id,
                        Data = raw,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Data = raw;
                    existing.Updated = DateTime.UtcNow;
                    CoreHub.ValueManager.Update(existing);
                }
            }
        }

        /// <summary>
        /// Brings a serialized payload into the canonical storage form of its field type.
        /// </summary>
        /// <remarks>
        /// Only tags need it. A tag list is stored comma-separated, which is what the
        /// object detail page writes and reads, but the tag input control of the table
        /// cells submits its tags semicolon-separated. Rewriting the separator here keeps
        /// one shape in the value row no matter which surface wrote it.
        /// </remarks>
        /// <param name="raw">The serialized payload.</param>
        /// <param name="fieldType">The type of the field being written.</param>
        /// <returns>The payload in storage form.</returns>
        private static string Normalize(string raw, FieldType fieldType)
        {
            if (fieldType != FieldType.Tag || string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            return string.Join(",", raw
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        /// <summary>
        /// Serializes a single field-payload value into the string form persisted in
        /// <see cref="Model.Entities.Value.Data"/>. Tag-style list payloads collapse to
        /// a comma-separated representation that matches the parse logic of
        /// <c>ObjectItemDetailFragment.BuildInputValue</c>.
        /// </summary>
        private static string SerializePayloadValue(object value)
        {
            return value switch
            {
                null => null,
                string s => s,
                bool b => b ? "true" : "false",
                System.Collections.IEnumerable list and not string => string.Join
                (
                    ",",
                    list.Cast<object>().Where(x => x is not null).Select(x => x.ToString())
                ),
                _ => value.ToString()
            };
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
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Object existingItem, IRequest request)
        {
            CoreHub.ObjectManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
