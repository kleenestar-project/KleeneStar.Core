using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The storage policy of the avatar dialogs, applied through the image hooks of
    /// <see cref="RestApiCrud{TIndexItem}"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="RestApiCrud{TIndexItem}"/> takes every inline or emptied picture of a property
    /// bound by <c>RestValueConverterImageIcon</c> out of the payload, checks it against
    /// <c>ImageOptions</c> and hands it to <c>StoreImage</c> or <c>RemoveImage</c>; the address
    /// <c>StoreImage</c> answers is bound to the property, an emptied picture binds
    /// <see langword="null"/>. An endpoint behind an avatar dialog overrides the three members
    /// with <see cref="Options"/>, <see cref="Store"/> and <see cref="Remove"/> and sets its
    /// fallback icon after the binding where the property is <see langword="null"/>.
    /// <para>
    /// A picture is stored or removed on an update (<c>PUT</c>, <c>PATCH</c>) only. On a create
    /// the record has no id that could name the file, and on a clone the hooks receive the source
    /// record, whose pictures must stay; the create and clone paths assign a generated icon.
    /// </para>
    /// </remarks>
    public static class AvatarStorage
    {
        /// <summary>
        /// Gets the image formats and limits the application icon route accepts.
        /// </summary>
        public static RestApiAvatarUploadOptions Options
        {
            get
            {
                var options = new RestApiAvatarUploadOptions();
                options.Formats["image/jpg"] = ".jpg";
                options.Formats["image/svg+xml"] = ".svg";

                return options;
            }
        }

        /// <summary>
        /// Stores a picture for a record and answers the address of the stored file.
        /// </summary>
        /// <param name="ownerId">The record the picture belongs to; it names the file.</param>
        /// <param name="image">The decoded and checked picture.</param>
        /// <param name="request">The request; only an update stores.</param>
        /// <returns>The address, or <see langword="null"/> when nothing was stored.</returns>
        public static string Store(Guid? ownerId, RestApiAvatarUpload image, IRequest request)
        {
            if (ownerId is not Guid id || id == Guid.Empty || image is null || !IsUpdate(request))
            {
                return null;
            }

            return CoreHub.StoreIcon(id, image)?.Uri?.ToString();
        }

        /// <summary>
        /// Removes the stored pictures of a record.
        /// </summary>
        /// <param name="ownerId">The record the pictures belong to.</param>
        /// <param name="request">The request; only an update removes.</param>
        public static void Remove(Guid? ownerId, IRequest request)
        {
            if (ownerId is Guid id && id != Guid.Empty && IsUpdate(request))
            {
                CoreHub.RemoveStoredIcons(id);
            }
        }

        /// <summary>
        /// Stores the inline picture a new record was bound with under its own id.
        /// </summary>
        /// <remarks>
        /// On a create or clone <see cref="Store"/> stores nothing, so the binding leaves an
        /// inline picture as a <c>data:</c> address on the record. This writes it to the icons
        /// directory under the new id; a record without a picture, or with one that does not
        /// decode, takes the fallback.
        /// </remarks>
        /// <param name="ownerId">The id of the new record.</param>
        /// <param name="icon">The picture the record was bound with.</param>
        /// <param name="fallback">The picture of a record without one.</param>
        /// <returns>The picture to store on the record.</returns>
        public static ImageIcon Adopt(Guid ownerId, ImageIcon icon, Func<ImageIcon> fallback)
        {
            var address = icon?.Uri?.ToString();

            if (string.IsNullOrWhiteSpace(address))
            {
                return fallback?.Invoke();
            }

            if (!address.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return icon;
            }

            var field = new RestApiCrudFormData { ["Icon"] = address }.DetachAvatar("Icon", Options, new RestApiValidationResult());

            return field.State == RestApiFormFieldState.Valid
                ? CoreHub.StoreIcon(ownerId, field.Value) ?? fallback?.Invoke()
                : fallback?.Invoke();
        }

        /// <summary>
        /// Refuses a picture address the dialog did not load.
        /// </summary>
        /// <remarks>
        /// The image hooks of <see cref="RestApiCrud{TIndexItem}"/> check inline pictures
        /// (<c>data:</c>, <c>file:</c>) and empty entries; any other value is bound as an address.
        /// An address other than the one the record carries is reported as an invalid picture, so
        /// a client cannot point a record at a foreign image.
        /// </remarks>
        /// <param name="payload">The submitted form fields.</param>
        /// <param name="name">The image field name.</param>
        /// <param name="result">The validation result receiving the error.</param>
        /// <param name="current">The picture the record carries.</param>
        /// <returns>The supplied result.</returns>
        public static IRestApiValidationResult Validate(RestApiCrudFormData payload, string name, IRestApiValidationResult result, ImageIcon current = null)
        {
            if (payload is null || !payload.TryGetValue(name, out var value))
            {
                return result;
            }

            var text = value?.ToString();

            if (string.IsNullOrWhiteSpace(text)
                || text.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || (current is not null && string.Equals(text, current.Uri?.ToString(), StringComparison.Ordinal)))
            {
                return result;
            }

            // the framework reports an address as an invalid picture
            new RestApiCrudFormData { [name] = value }.DetachAvatar(name, Options, result);

            return result;
        }

        /// <summary>
        /// Removes a field from the payload and hands back what it carried.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="name">
        /// The field name as declared on the entity (e.g. <c>Icon</c>); the lookup is
        /// case-insensitive, matching the lower-cased keys the payload parser produces.
        /// </param>
        /// <param name="submitted">
        /// When this method returns, contains the submitted value, or an empty string for an
        /// empty entry.
        /// </param>
        /// <returns><see langword="true"/> when the payload carried the field.</returns>
        public static bool Detach(this RestApiCrudFormData fieldMap, string name, out string submitted)
        {
            submitted = null;

            if (fieldMap is null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            var field = fieldMap.Detach<string>(name);
            submitted = field.State == RestApiFormFieldState.Empty ? string.Empty : field.Value;

            return field.State != RestApiFormFieldState.Missing;
        }

        /// <summary>
        /// Reads a field from the payload without removing it, answering both its value and
        /// whether the payload carried it at all.
        /// </summary>
        /// <param name="fieldMap">The payload to read from. May be null.</param>
        /// <param name="name">The field name as declared on the entity (e.g. <c>Renderer</c>).</param>
        /// <returns>The value and whether the payload carried the field.</returns>
        public static (string Value, bool Sent) TryRead(this RestApiCrudFormData fieldMap, string name)
        {
            if (fieldMap is null || string.IsNullOrEmpty(name))
            {
                return (null, false);
            }

            var field = fieldMap.TryRead<string>(name);

            return (field.State == RestApiFormFieldState.Empty ? string.Empty : field.Value,
                field.State != RestApiFormFieldState.Missing);
        }

        /// <summary>
        /// Determines whether a request updates a record.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for <c>PUT</c> and <c>PATCH</c>.</returns>
        private static bool IsUpdate(IRequest request)
        {
            return request?.Method is RequestMethod.PUT or RequestMethod.PATCH;
        }
    }
}
