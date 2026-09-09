using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Takes the picture an avatar control submitted out of a CRUD payload and turns it into
    /// the icon the entity should carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WebExpress.WebUI.WebControl.ControlFormItemInputAvatar"/> has no upload
    /// endpoint of its own; it posts the picture inline as part of the form value, shaped as
    /// <c>file:&lt;name&gt;;data:&lt;mime&gt;;base64,&lt;payload&gt;</c>. The property behind it
    /// is an <see cref="ImageIcon"/>, which holds a URI and nothing else, and the converter in
    /// between hands the whole string to <c>ImageIcon.FromString</c> — the data url is parsed as
    /// a URI and collapses to <c>http:///</c>. <b>The request still answers 200</b>, so an
    /// endpoint that forgets this loses the picture without anything reporting a fault: the
    /// dialog closes, the avatar is gone, and the record now points at a URI that serves
    /// nothing.
    /// </para>
    /// <para>
    /// The two steps below are therefore both required, and in this order: <see cref="Detach"/>
    /// removes the field <em>before</em> <c>base.Update</c> lets the binder near it, and
    /// <see cref="Resolve"/> decodes what was removed and points the entity at the stored file.
    /// This helper exists so the six avatar dialogs of the application - class, workspace,
    /// template, tenant, profile and branding - share one implementation instead of six copies
    /// that drift apart, which is exactly how four of them came to be broken.
    /// </para>
    /// </remarks>
    public static class RestApiCrudFormDataAvatarExtensions
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

            if (fieldMap.Remove(name.ToLowerInvariant(), out var value) ||
                fieldMap.Remove(name, out value))
            {
                submitted = value as string ?? value?.ToString();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Answers the icon an entity should carry after its avatar dialog was submitted:
        /// stores the picture and points at the file, or falls back when it was removed.
        /// </summary>
        /// <remarks>
        /// A payload that carries no usable image - a media type the icon route cannot serve, a
        /// file past the size limit, something that is not a data url at all - leaves the
        /// current picture alone rather than clearing it. The user asked to change the avatar,
        /// not to lose it.
        /// </remarks>
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
            if (string.IsNullOrWhiteSpace(submitted))
            {
                CoreHub.RemoveStoredIcons(ownerId);

                return fallback?.Invoke();
            }

            return CoreHub.StoreIcon(ownerId, submitted) ?? current;
        }
    }
}
