using WebExpress.WebCore.WebRestApi;
using KleeneStar.Model;
using System.Collections.Generic;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Branding
{
    /// <summary>
    /// Serves the identity of the installation - the title and the icon the application is
    /// presented under - to the settings form and takes its updates.
    /// </summary>
    /// <remarks>
    /// The identity is a singleton, so only the retrieve and update halves of the CRUD contract
    /// are meaningful here. Creating or deleting is left to the inherited implementations, which
    /// the settings form never calls because it addresses the fixed record directly.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Branding>
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
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        /// <summary>
        /// Changes the installation's identity, once the caller administers the installation.
        /// </summary>
        /// <remarks>
        /// The record is what the application is presented under and what a visitor who is not
        /// signed in reads on the start page - a write open to anybody is a defacement open to
        /// anybody. The administrators are those of the accounts (<see cref="AccountAuthorization"/>),
        /// the only installation-wide administration there is.
        /// </remarks>
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
        /// Validates the complete request before binding or storing an avatar.
        /// </summary>
        /// <param name="existingItem">The existing record, or null for creation.</param>
        /// <param name="payload">The submitted fields.</param>
        /// <param name="request">The current request.</param>
        /// <returns>The field validation result.</returns>
        protected override IRestApiValidationResult Validate(Model.Entities.Branding existingItem, RestApiCrudFormData payload, IRequest request)
        {
            return AvatarStorage.Validate(payload, nameof(Model.Entities.Branding.Icon), base.Validate(existingItem, payload, request), existingItem?.Icon);
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the branding records that match the specified query criteria.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered set of records; may be empty.</returns>
        protected override IEnumerable<Model.Entities.Branding> Retrieve(IQuery<Model.Entities.Branding> query, IQueryContext context, IRequest request)
        {
            return CoreHub.BrandingManager.GetBrandings(query, context);
        }

        /// <summary>
        /// Retrieves the branding in preparation for an update.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="request">The request context.</param>
        /// <returns>An object containing the branding.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Branding> query, IRequest request)
        {
            // the record is the singleton the manager already holds, so the query is not applied
            // here and a fresh installation yields the empty default rather than nothing
            return RetrieveForUpdate(request, CoreHub.BrandingManager.GetBranding());
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
        protected override string StoreImage(Model.Entities.Branding item, string field, RestApiAvatarUpload image, IRequest request)
        {
            return AvatarStorage.Store(item is null ? null : Model.Entities.Branding.SingletonId, image, request);
        }

        /// <summary>
        /// Removes the stored pictures when an update empties the picture (<see cref="AvatarStorage.Remove"/>).
        /// </summary>
        /// <param name="item">The persisted record; the source on a clone, null on a create.</param>
        /// <param name="field">The property whose picture is removed.</param>
        /// <param name="request">The HTTP request.</param>
        protected override void RemoveImage(Model.Entities.Branding item, string field, IRequest request)
        {
            AvatarStorage.Remove(item is null ? null : Model.Entities.Branding.SingletonId, request);
        }

        /// <summary>
        /// Persists the edited branding and applies it to the running application.
        /// </summary>
        /// <param name="existingItem">The currently persisted branding.</param>
        /// <param name="payload">The dynamic payload containing the edited branding.</param>
        /// <param name="request">The HTTP request providing additional context.</param>
        /// <returns>A result object containing information about the update operation.</returns>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Branding existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // an emptied icon binds null; the application then shows the icon its [Icon] attribute declares
            var res = base.Update(existingItem, payload, request);

            CoreHub.BrandingManager.Update(existingItem);

            return res;
        }
    }
}
