using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Applies application storage and fallback policies to framework avatar submissions.
    /// </summary>
    public static class AvatarStorage
    {
        /// <summary>
        /// Removes the avatar field from the payload and hands back what it carried, so the
        /// CRUD binder never sees the inline picture.
        /// </summary>
        /// <param name="fieldMap">The payload to take the picture out of.</param>
        /// <param name="name">
        /// The field name as declared on the entity (e.g. <c>Icon</c>); the lookup is
        /// case-insensitive, matching the lower-cased keys the payload parser produces.
        /// </param>
        /// <param name="submitted">
        /// When this method returns, contains the submitted value: the inline picture, or an
        /// empty string when the dialog reported that the picture was removed.
        /// </param>
        /// <returns>
        /// True when the payload carried the field at all. False means the form does not edit
        /// the avatar, and the current icon must be left exactly as it is - which is not the
        /// same as an empty value, and is why this is reported separately.
        /// </returns>
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
        /// Answers the icon an entity should carry after its avatar dialog was submitted:
        /// stores the picture and points at the file, or falls back when it was removed.
        /// </summary>
        /// <param name="ownerId">
        /// The entity the picture belongs to; it names the file and scopes the cleanup, so it
        /// must be the id of the record being saved.
        /// </param>
        /// <param name="submitted">The value <see cref="Detach"/> handed back.</param>
        /// <param name="current">The icon the entity carries now.</param>
        /// <param name="fallback">
        /// What the entity falls back to when the picture was removed - the generated initials
        /// icon for most records, <see langword="null"/> where the application supplies its own
        /// default. It is a factory rather than a value because generating an icon writes a
        /// file, which must not happen on a save that removes nothing.
        /// </param>
        /// <returns>The icon to store.</returns>
        public static ImageIcon Resolve(Guid ownerId, string submitted, ImageIcon current, Func<ImageIcon> fallback)
        {
            if (current is not null && string.Equals(submitted, current.Uri?.ToString(), StringComparison.Ordinal))
            {
                return current;
            }
            var fields = new RestApiCrudFormData { ["Icon"] = submitted };
            var validation = new RestApiValidationResult();
            var field = fields.DetachAvatar("Icon", Options(), validation);
            return field.ResolveAvatar(current, upload => CoreHub.StoreIcon(ownerId, upload),
                image => image, () => CoreHub.RemoveStoredIcons(ownerId), fallback);
        }
        /// <summary>
        /// Validates an avatar without consuming the payload before the complete form is accepted.
        /// </summary>
        /// <param name="payload">The submitted form fields.</param>
        /// <param name="name">The image field name.</param>
        /// <param name="result">The validation result receiving upload errors.</param>
        /// <param name="current">The existing image accepted as an unchanged control value.</param>
        /// <returns>The supplied result including upload validation.</returns>
        public static IRestApiValidationResult Validate(RestApiCrudFormData payload, string name, IRestApiValidationResult result, ImageIcon current = null)
        {
            var copy = new RestApiCrudFormData();
            if (payload.TryGetValue(name, out var value))
            {
                if (current is not null && string.Equals(value?.ToString(), current.Uri?.ToString(), StringComparison.Ordinal))
                {
                    return result;
                }
                copy[name] = value;
            }
            copy.DetachAvatar(name, Options(), result);
            return result;
        }

        /// <summary>
        /// Retains the image formats supported by the application icon route.
        /// </summary>
        /// <returns>The application upload limits.</returns>
        private static RestApiAvatarUploadOptions Options()
        {
            var options = new RestApiAvatarUploadOptions();
            options.Formats["image/jpg"] = ".jpg";
            options.Formats["image/svg+xml"] = ".svg";
            return options;
        }
    }
}
