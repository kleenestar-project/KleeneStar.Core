using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Provides CRUD operations for workspace items via a REST API.
    /// </summary>
    [Cache]
    public sealed partial class Index : RestApiCrud<Workspace>
    {
        /// <summary>
        /// The shape a workspace key has to have: it becomes the prefix of every object key in
        /// the workspace (<c>SD-17</c>) and a segment of its URLs, so it stays short and free of
        /// anything that would have to be escaped.
        /// </summary>
        /// <remarks>
        /// Upper case is allowed because the keys the product itself proposes are upper case -
        /// the seeded workspaces and every template's <c>SuggestedKey</c>. A pattern that refused
        /// them would refuse the wizard's own suggestion.
        /// </remarks>
        [GeneratedRegex(@"^[a-zA-Z0-9-]{1,10}$")]
        private static partial Regex KeyRegex();

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
            return base.Retrieve(request);
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
        protected override IEnumerable<Workspace> Retrieve(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            return CoreHub.WorkspaceManager.GetWorkspaces(query, context);
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
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">The request.</param>
        /// <returns>
        /// A result instance representing the data and metadata required
        /// to initialize a new item for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Workspace> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.WorkspaceManager.GetWorkspaces(query, context)
                .FirstOrDefault();

            var newItem = new Workspace()
            {
                Key = data.Key + "-copy",
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Categories = data.Categories,
                Icon = data.Icon,
                State = WorkspaceState.Active,
                InheritedId = data.InheritedId,
                Sealed = data.Sealed,
                AccessModifier = data.AccessModifier,
                Tenants = data.Tenants
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a workspace identified by the specified key for update operations.
        /// </summary>
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
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Workspace> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.WorkspaceManager.GetWorkspaces(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
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
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Workspace> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.WorkspaceManager.GetWorkspaces(query, context)
                .FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Key);
        }

        /// <summary>
        /// Validate the data for create or update operations. When creating, existingItem will
        /// be null and proposedItem contains the values to create. When updating, existingItem
        /// is the currently persisted entity and proposedItem contains the incoming values to
        /// validate.
        /// </summary>
        /// <remarks>
        /// The base validation reads the <c>Validate…</c> attributes of the entity, and it reads
        /// them <b>per field the payload carries</b> - a field the payload leaves out is not
        /// checked, because for an update "not mentioned" means "unchanged". On a create there is
        /// nothing to leave unchanged, so the two fields a workspace cannot exist without are
        /// demanded here whether the payload mentions them or not. Without this, a create that
        /// simply omits the name answers 200 and leaves a row no list can address.
        /// <para>
        /// Uniqueness is checked here as well rather than only by
        /// <see cref="UniqueKey"/> / <see cref="UniqueName"/>. Those endpoints serve the form
        /// while it is being filled in; they are advice, not a gate, and every caller that is not
        /// the form skips them entirely.
        /// </para>
        /// </remarks>
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
        protected override IRestApiValidationResult Validate(Workspace existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var creating = existingItem is null;

            var (key, keySent) = ReadField(payload, nameof(Workspace.Key));
            var (name, nameSent) = ReadField(payload, nameof(Workspace.Name));

            if (creating || keySent)
            {
                ValidateKey(result, key, keySent, existingItem, request);
            }

            if (creating || nameSent)
            {
                ValidateName(result, name, nameSent, existingItem, request);
            }

            return result;
        }

        /// <summary>
        /// Checks the submitted key: present, of a usable shape, not one of the routes the
        /// application owns, and not already taken.
        /// </summary>
        /// <param name="result">The result the errors are collected in.</param>
        /// <param name="key">The submitted key.</param>
        /// <param name="sent">Whether the payload carried the field at all.</param>
        /// <param name="existingItem">The persisted workspace on an update, otherwise null.</param>
        /// <param name="request">The request, for the culture the messages are written in.</param>
        private static void ValidateKey(IRestApiValidationResult result, string key, bool sent, Workspace existingItem, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                // a field the payload carried has already been reported blank by the entity's own
                // ValidateRequired; saying it twice reads as two separate faults
                if (!sent)
                {
                    result.Add(Translate(request, "kleenestar.core:workspace.key.validation.required"), nameof(Workspace.Key));
                }

                return;
            }

            key = key.Trim();

            if (WorkspaceManager.ReservedWorkspaceKeys.Contains(key.ToLowerInvariant()))
            {
                result.Add(Translate(request, "kleenestar.core:workspace.key.validation.reserved"), nameof(Workspace.Key));

                return;
            }

            if (!KeyRegex().IsMatch(key))
            {
                result.Add(Translate(request, "kleenestar.core:workspace.key.validation.pattern"), nameof(Workspace.Key));

                return;
            }

            if (IsTaken(x => x.WhereEqualsIgnoreCase(y => y.Key, key), existingItem))
            {
                result.Add(Translate(request, "kleenestar.core:workspace.key.validation.taken"), nameof(Workspace.Key));
            }
        }

        /// <summary>
        /// Checks the submitted name: present and not already taken.
        /// </summary>
        /// <param name="result">The result the errors are collected in.</param>
        /// <param name="name">The submitted name.</param>
        /// <param name="sent">Whether the payload carried the field at all.</param>
        /// <param name="existingItem">The persisted workspace on an update, otherwise null.</param>
        /// <param name="request">The request, for the culture the messages are written in.</param>
        private static void ValidateName(IRestApiValidationResult result, string name, bool sent, Workspace existingItem, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                // see ValidateKey: a field that was sent blank is already reported by the entity's
                // own ValidateRequired
                if (!sent)
                {
                    result.Add(Translate(request, "kleenestar.core:workspace.name.validation.required"), nameof(Workspace.Name));
                }

                return;
            }

            name = name.Trim();

            if (IsTaken(x => x.WhereEqualsIgnoreCase(y => y.Name, name), existingItem))
            {
                result.Add(Translate(request, "kleenestar.core:workspace.name.validation.taken"), nameof(Workspace.Name));
            }
        }

        /// <summary>
        /// Determines whether a workspace other than the one being edited already matches the
        /// supplied criteria.
        /// </summary>
        /// <param name="criteria">Narrows the query to the value being checked.</param>
        /// <param name="existingItem">The workspace being edited, which never counts against
        /// itself, or null on a create.</param>
        /// <returns><see langword="true"/> when the value is already in use.</returns>
        private static bool IsTaken(Func<IQuery<Workspace>, IQuery<Workspace>> criteria, Workspace existingItem)
        {
            using var context = ModelHub.CreateDbContext();

            return CoreHub.WorkspaceManager
                .GetWorkspaces(criteria(new Query<Workspace>()), context)
                .Any(x => existingItem is null || x.Id != existingItem.Id);
        }

        /// <summary>
        /// Reads a field out of the submitted payload.
        /// </summary>
        /// <remarks>
        /// The payload parser lower-cases every property name it reads off the wire, so a lookup
        /// spelled the way the property is declared misses - silently, because a missing key is
        /// indistinguishable from an unsent field. Both spellings are tried so the endpoint reads
        /// the same value whether the form or a hand-written client sent it.
        /// </remarks>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="field">The name of the field, as the property spells it.</param>
        /// <returns>The value, and whether the payload carried the field at all - which is a
        /// different thing from carrying it empty, and the two are answered differently.</returns>
        private static (string Value, bool Sent) ReadField(RestApiCrudFormData payload, string field)
        {
            if (payload is null)
            {
                return (null, false);
            }

            if (payload.TryGetValue(field.ToLowerInvariant(), out var lower))
            {
                return (lower?.ToString(), true);
            }

            return payload.TryGetValue(field, out var exact) ? (exact?.ToString(), true) : (null, false);
        }

        /// <summary>
        /// Translates a message into the culture of the request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="key">The internationalization key.</param>
        /// <returns>The translated message.</returns>
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
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Workspace newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Workspace(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = WorkspaceState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.WorkspaceManager.Add(newItem);

            ApplyTemplate(fieldMap, newItem, request);
            RecordCreationAsVisit(newItem, request);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Records the creation as the creator's most recent visit to the workspace.
        /// </summary>
        /// <remarks>
        /// The workspace dropdown is a list of recently visited workspaces, and a visit is
        /// stamped by the workspace pages when they are opened. A workspace that was just
        /// created has been opened by nobody, so without this it is the one workspace missing
        /// from the menu of the person who made it - and it stays missing until they navigate
        /// to it, which is what the menu was to be used for.
        /// <para>
        /// Creating a workspace is treated as the first visit rather than as a special case in
        /// the dropdown, because the reading is the honest one - the creator was just there,
        /// in the wizard - and because it holds for every caller that creates a workspace, not
        /// only for the wizard the menu offers.
        /// </para>
        /// </remarks>
        /// <param name="workspace">The workspace that was just created.</param>
        /// <param name="request">The request naming the identity that created it.</param>
        private static void RecordCreationAsVisit(Workspace workspace, IRequest request)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (ownerId != Guid.Empty && workspace is not null)
            {
                CoreHub.WorkspaceManager.RecordVisit(ownerId, workspace.Id);
            }
        }

        /// <summary>
        /// Sets the workspace up from the template the creation wizard chose: its classes, the
        /// starting views of its issue and asset overviews, its home page and the post announcing
        /// it.
        /// </summary>
        /// <remarks>
        /// It runs after the workspace rather than with it, because everything it creates belongs
        /// to the workspace: there is nothing to attach any of it to until it exists, and a
        /// workspace that was stored while its setup failed is one an administrator can finish by
        /// hand - the other order would leave classes belonging to nothing.
        /// <para>
        /// A payload without the field, or carrying the wizard's "no template" value, creates
        /// none of it. That is the ordinary case for every caller that is not the wizard - the
        /// REST API itself, an import - and it must stay the default rather than an error.
        /// </para>
        /// </remarks>
        /// <param name="fieldMap">The submitted form data.</param>
        /// <param name="workspace">The workspace that was just created.</param>
        /// <param name="request">The request, naming the identity the two pages are authored by.</param>
        private static void ApplyTemplate(RestApiCrudFormData fieldMap, Workspace workspace, IRequest request)
        {
            // the payload parser lower-cases every property name it reads off the wire, so a
            // lookup spelled the way the field is declared misses - silently, because a missing
            // key is indistinguishable from an unsent field
            if (fieldMap is null ||
                !fieldMap.TryGetValue(WebFragment.Workspace.WorkspaceAddFormFragment.TemplateField.ToLowerInvariant(), out var value))
            {
                return;
            }

            var key = value?.ToString();

            if (string.IsNullOrWhiteSpace(key) ||
                string.Equals(key, WebFragment.Workspace.WorkspaceAddFormFragment.NoTemplate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CoreHub.WorkspaceTemplateManager.Apply
            (
                key,
                workspace.Id,
                CoreHub.SessionManager.GetCurrentIdentityId(request),

                // the pages are written once and read as they were written, so they are written
                // in the language of whoever is creating the workspace rather than the
                // installation's default - which on this installation is not the one the wizard
                // was just filled in in
                request?.Culture
            );
        }

        /// <summary>
        /// Creates a new workspace instance by cloning data from the specified form fields and 
        /// adds it to the workspace manager.
        /// </summary>
        /// <param name="existingItem">
        /// The existing workspace item to use as a reference for the clone operation. This parameter 
        /// is not modified.
        /// </param>
        /// <param name="fieldMap">
        /// The form data containing field values to bind to the new workspace instance. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The current request context for the operation. Provides additional information or 
        /// services required during cloning.
        /// </param>
        /// <param name="newItem">
        /// When this method returns, contains the newly created workspace instance populated 
        /// with the provided form data.
        /// </param>
        /// <returns>
        /// A result object indicating the outcome of the create operation.
        /// </returns>
        protected override IRestApiCrudResultCreate Clone(Workspace existingItem, RestApiCrudFormData fieldMap, IRequest request, out Workspace newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Workspace(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = WorkspaceState.Active
            };

            fieldMap.BindTo(newItem);

            CoreHub.WorkspaceManager.Add(newItem);

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
        protected override IRestApiCrudResultUpdate Update(Workspace existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // the avatar dialog posts its picture inline as a data url, which the binder would
            // hand to RestValueConverterImageIcon and collapse to the URI "http:///" - while the
            // request still answers 200. So it is taken out of the payload before the binder sees
            // it, decoded, and written to the icons directory. See RestApiCrudFormDataAvatarExtensions.
            var avatarSent = payload.Detach(nameof(Workspace.Icon), out var avatar);

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

            CoreHub.WorkspaceManager.Update(existingItem);

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
        protected override IRestApiCrudResultDelete Delete(Workspace existingItem, IRequest request)
        {
            CoreHub.WorkspaceManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
