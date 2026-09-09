using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Classes
{
    /// <summary>
    /// Provides CRUD operations for class items via a REST API.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Class>
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
        protected override IEnumerable<Model.Entities.Class> Retrieve(IQuery<Model.Entities.Class> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ClassManager.GetClasses(query, context);
        }

        /// <summary>
        /// Retrieves the data required to create a new class entity.
        /// </summary>
        /// <param name="request">
        /// The request context containing parameters and metadata for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the information necessary to initialize a new class for creation.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForCreate(IRequest request)
        {
            return RetrieveForCreate(request);
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
        protected override IRestApiCrudResultRetrieve RetrieveForClone(IQuery<Model.Entities.Class> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ClassManager.GetClasses(query, context)
                .FirstOrDefault();

            var newItem = new Model.Entities.Class()
            {
                Name = data.Name + " (Copy)",
                Description = data.Description,
                Icon = data.Icon,
                State = ClassState.Active,
                IsAbstract = data.IsAbstract,
                Sealed = data.Sealed,
                InheritedId = data.InheritedId,
                ParentId = data.ParentId,
                AccessModifier = data.AccessModifier,
                Kind = data.Kind,
                Renderer = data.Renderer
            };

            return RetrieveForClone(request, newItem);
        }

        /// <summary>
        /// Retrieves a class identified by the specified key for update operations.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="request">
        /// The request context containing additional information for the retrieval operation.
        /// </param>
        /// <returns>
        /// An object containing the class associated with the specified key.
        /// </returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Class> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ClassManager.GetClasses(query, context)
                .FirstOrDefault();

            return RetrieveForUpdate(request, data);
        }

        /// <summary>
        /// Retrieves the class entity identified by the specified ID in preparation for deletion.
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
        /// An object containing the class entity and related information required 
        /// for the delete operation.
        /// </returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<Model.Entities.Class> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ClassManager.GetClasses(query, context)
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
        protected override IRestApiValidationResult Validate(Model.Entities.Class existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);

            var (submitted, rendererSent) = ReadField(payload, nameof(Model.Entities.Class.Renderer));
            var renderer = WebFragment.Object.ObjectRendererCatalog.Unwrap(submitted);

            if (rendererSent)
            {
                // the kind may be changing in the same payload; a field it does not carry is
                // unchanged, so the persisted one is what the renderer has to fit
                var (kind, kindSent) = ReadField(payload, nameof(Model.Entities.Class.Kind));
                var effective = kindSent ? kind : existingItem?.Kind;

                ValidateRenderer(result, effective, renderer, request);
            }

            return result;
        }

        /// <summary>
        /// Checks the submitted renderer against the object type it would apply to. An unset
        /// renderer is always accepted - it is the class saying "follow the object type" -
        /// and so is one the type offers; anything else is refused, naming what the type does
        /// offer, because a renderer no fragment is gated on would leave the object with no
        /// reading view at all.
        /// </summary>
        /// <param name="result">The result the errors are collected in.</param>
        /// <param name="kind">The object-kind key the class will carry.</param>
        /// <param name="renderer">The submitted renderer key.</param>
        /// <param name="request">The request, for the culture the message is written in.</param>
        private static void ValidateRenderer(IRestApiValidationResult result, string kind, string renderer, IRequest request)
        {
            if (WebFragment.Object.ObjectRendererCatalog.IsOffered(kind, renderer))
            {
                return;
            }

            var offered = string.Join
            (
                ", ",
                WebFragment.Object.ObjectRendererCatalog.GetRenderers(kind)
                    .Select(x => Translate(request, x.Label))
            );

            result.Add
            (
                string.Format(Translate(request, "kleenestar.core:class.renderer.validation.unsupported"), offered),
                nameof(Model.Entities.Class.Renderer)
            );
        }

        /// <summary>
        /// Reads a field from the payload, answering both its value and whether the payload
        /// carried it at all - "absent" and "sent empty" mean different things on an update.
        /// </summary>
        /// <param name="payload">The payload to read from.</param>
        /// <param name="field">The name of the field.</param>
        /// <returns>The value and whether it was sent.</returns>
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
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Class newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Class(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = ClassState.Active
            };

            fieldMap.BindTo(newItem);

            // the picker's 'follow the object type' entry travels as a token because an option
            // cannot carry an empty value; it becomes the unset state here, before persistence
            newItem.Renderer = WebFragment.Object.ObjectRendererCatalog.Unwrap(newItem.Renderer);

            CoreHub.ClassManager.Add(newItem);

            // automatically create the standard form for the new class
            CoreHub.FormManager.CreateStandardForm(newItem.Id);

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
        protected override IRestApiCrudResultCreate Clone(Model.Entities.Class existingItem, RestApiCrudFormData fieldMap, IRequest request, out Model.Entities.Class newItem)
        {
            var id = Guid.NewGuid();
            newItem = new Model.Entities.Class(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = ClassState.Active
            };

            fieldMap.BindTo(newItem);

            newItem.Renderer = WebFragment.Object.ObjectRendererCatalog.Unwrap(newItem.Renderer);

            CoreHub.ClassManager.Add(newItem);

            // automatically create the standard form for the cloned class
            CoreHub.FormManager.CreateStandardForm(newItem.Id);

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
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Class existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // the avatar dialog posts its picture inline as a data url, which the binder would
            // hand to RestValueConverterImageIcon and collapse to the URI "http:///" - while the
            // request still answers 200. So it is taken out of the payload before the binder sees
            // it, decoded, and written to the icons directory. See RestApiCrudFormDataAvatarExtensions.
            var avatarSent = payload.Detach(nameof(Model.Entities.Class.Icon), out var avatar);

            var res = base.Update(existingItem, payload, request);

            existingItem.Renderer = WebFragment.Object.ObjectRendererCatalog.Unwrap(existingItem.Renderer);

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

            CoreHub.ClassManager.Update(existingItem);

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
        protected override IRestApiCrudResultDelete Delete(Model.Entities.Class existingItem, IRequest request)
        {
            CoreHub.ClassManager.Remove(existingItem.Id);

            return base.Delete(existingItem, request);
        }
    }
}
