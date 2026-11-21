using KleeneStar.Core.WebParameter;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;

// The entity type Object collides with System.Object; alias it so the signatures read
// naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_
{
    /// <summary>
    /// Table endpoint of the issue overview. The table logic — the column catalog, the
    /// per-identity layout, filtering, sorting and paging — lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindTable"/>; this
    /// endpoint scopes it to the issue kind and contributes the row menu.
    /// </summary>
    [Cache]
    public sealed class Table : global::KleeneStar.Core.WebRestApi.RestApiObjectKindTable
    {
        /// <summary>
        /// Gets the object kind the table is scoped to: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the key the user-defined quickfilters of the issue views are stored under.
        /// </summary>
        protected override string ViewKey => Quickfilter.ViewKey;

        /// <summary>
        /// Builds the row overflow menu: edit and clone modals, the star toggle (whose label
        /// reflects the current state and whose link flips it), and the delete modal.
        /// </summary>
        /// <param name="entity">The issue the options act on. Cannot be null.</param>
        /// <param name="starred">Whether the calling identity has starred the issue.</param>
        /// <param name="request">The request used to resolve localized labels and URIs.</param>
        /// <returns>The overflow menu options.</returns>
        protected override IEnumerable<RestApiOption> GetOptions(ObjectEntity entity, bool starred, IRequest request)
        {
            var keyParameter = new ObjectKeyParameter(entity.Key);
            var editUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Edit>()?
                .BindParameters(request)
                .BindParameters(keyParameter);
            var cloneUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Clone>()?
                .BindParameters(request)
                .BindParameters(keyParameter);
            var deleteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Delete>()?
                .BindParameters(request)
                .BindParameters(keyParameter);
            var favoriteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Favorite>()?
                .BindParameters(request)
                .BindParameters(keyParameter);


            yield return new RestApiOptionHeader(request)
            {
                Text = "webexpress.webapp:header.setting.label"
            };

            yield return new RestApiOptionEdit(request)
            {
                PrimaryAction = new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge)
            };

            yield return new RestApiOptionClone(request)
            {
                PrimaryAction = new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge)
            };

            // toggle the calling identity's star; the label reflects the current state
            // and the link flips it, redirecting back to the object detail page
            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, starred
                    ? "kleenestar.core:object.favorite.remove.label"
                    : "kleenestar.core:object.favorite.add.label"),
                Icon = new WebExpress.WebUI.WebIcon.IconStar(),
                Uri = favoriteUri
            };

            yield return new RestApiOptionSeparator(request);
            yield return new RestApiOptionDelete(request)
            {
                PrimaryAction = new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
            };
        }
    }
}
