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
            EnsureKey(newItem);
            ApplyDefaultSecurityLevel(fieldMap, newItem, currentUser);

            // the object row, its field values and the presets of its template are one act of
            // creation; the scope makes them one genesis commit rather than a create followed by
            // a handful of edits nobody performed
            using (CoreHub.CommitManager.BeginCommit(newItem.Id, CommitType.Created, currentUser))
            {
                CoreHub.ObjectManager.Add(newItem);

                UpsertFieldValues(newItem, fieldMap);

                ApplyTemplate(newItem, fieldMap, request);
            }

            return new RestApiCrudResultCreate();
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
            // submits as the empty guid
            if (fieldMap.ContainsKey(nameof(Model.Entities.Object.SecurityLevelId).ToLowerInvariant())
                || fieldMap.ContainsKey(nameof(Model.Entities.Object.SecurityLevelId))
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
        /// Applies the template the payload names to a freshly created object: its presets become
        /// field values, and each of its child templates becomes an object below the created one.
        /// </summary>
        /// <remarks>
        /// A value the caller submitted wins over the preset that would otherwise fill the same
        /// field — a template pre-fills a form, it does not overrule what the user typed into it.
        /// </remarks>
        /// <param name="object">The object that was created.</param>
        /// <param name="fieldMap">The payload, which may name a template.</param>
        /// <param name="request">The request, for resolving the acting identity.</param>
        private static void ApplyTemplate(Model.Entities.Object @object, RestApiCrudFormData fieldMap, IRequest request)
        {
            if (!fieldMap.TryGetGuid("TemplateId", out var templateId))
            {
                return;
            }

            var template = CoreHub.TemplateManager.GetTemplate(templateId);

            if (template is null || template.State != TemplateState.Active)
            {
                return;
            }

            ApplyPresets(@object, templateId, fieldMap);
            CreateChildren(@object, templateId, request, new HashSet<Guid> { templateId });
        }

        /// <summary>
        /// Writes the presets of a template as field values of an object, skipping the fields the
        /// payload already set.
        /// </summary>
        /// <param name="object">The object to write the values to.</param>
        /// <param name="templateId">The template whose presets are applied.</param>
        /// <param name="payload">The payload whose own values take precedence, or null.</param>
        private static void ApplyPresets(Model.Entities.Object @object, Guid templateId, RestApiCrudFormData payload)
        {
            var presets = new RestApiCrudFormData();

            foreach (var preset in CoreHub.TemplateManager.GetPresets(templateId))
            {
                var key = preset.Key.ToLowerInvariant();

                if (payload?.ContainsKey(key) == true)
                {
                    continue;
                }

                presets[key] = preset.Value;
            }

            UpsertFieldValues(@object, presets);
        }

        /// <summary>
        /// Creates one object per active child template below the supplied object, depth first and
        /// in the order the child templates define.
        /// </summary>
        /// <remarks>
        /// A child whose class the parent's class does not allow is skipped rather than created,
        /// so a composite template cannot build a hierarchy the object model would reject. The
        /// visited set carries the templates already instantiated along this branch, which keeps a
        /// cycle in the template hierarchy from creating objects without end.
        /// </remarks>
        /// <param name="parent">The object the created objects are placed below.</param>
        /// <param name="templateId">The template whose children are instantiated.</param>
        /// <param name="request">The request, for resolving the acting identity.</param>
        /// <param name="visited">The templates already instantiated along this branch.</param>
        private static void CreateChildren(Model.Entities.Object parent, Guid templateId, IRequest request, ISet<Guid> visited)
        {
            var parentClass = CoreHub.ClassManager.GetClass(parent.ClassId);
            var currentUser = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var author = ResolveAuthor(request);

            foreach (var childTemplate in CoreHub.TemplateManager.GetChildTemplates(templateId))
            {
                if (!visited.Add(childTemplate.Id))
                {
                    continue;
                }

                if (parentClass?.AllowedChildren is { Count: > 0 }
                    && parentClass.AllowedChildren.All(c => c.Id != childTemplate.ClassId))
                {
                    continue;
                }

                var id = Guid.NewGuid();
                var child = new Model.Entities.Object(id)
                {
                    Summary = childTemplate.Name,
                    Description = childTemplate.Description,
                    Icon = childTemplate.Icon ?? CoreHub.GenerateIcon(id),
                    State = WorkspaceState.Active,
                    ClassId = childTemplate.ClassId,
                    WorkspaceId = parent.WorkspaceId,
                    ParentId = parent.Id,
                    CreatorId = author,
                    UpdaterId = author
                };

                EnsureKey(child);

                // a child gets a genesis commit of its own carrying its presets, so its history
                // starts the same way a hand-created object's does
                using (CoreHub.CommitManager.BeginCommit(child.Id, CommitType.Created, currentUser))
                {
                    CoreHub.ObjectManager.Add(child);

                    ApplyPresets(child, childTemplate.Id, null);
                }

                CreateChildren(child, childTemplate.Id, request, visited);
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
